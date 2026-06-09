using For_the_Darkest_Dungeon.DefinitionDarkest;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text.RegularExpressions;

namespace For_the_Darkest_Dungeon.Classification
{
    internal class InfoErrorTagger : ITagger<IErrorTag>
    {
        private readonly ITextBuffer _buffer;

        private readonly Regex _keywordRegex = new Regex(@"\.[a-zA-Z_][a-zA-Z0-9_]*", RegexOptions.Compiled);
        private readonly Regex _headerRegex = new Regex(@"^[a-zA-Z0-9_]+:", RegexOptions.Compiled);
        private readonly Regex _stringRegex = new Regex(@"""[^""]*""", RegexOptions.Compiled);
        private readonly Regex _commentRegex = new Regex(@"//.*", RegexOptions.Compiled);

        private readonly HashSet<string> _allowedEffectsHeaders = new HashSet<string>
        {
            "riposte_skill:",
            "skill:",
            "combat_skill:",
            "combat_move_skill:"
        };

        internal InfoErrorTagger(ITextBuffer buffer)
        {
            _buffer = buffer;
            _buffer.Changed += OnBufferChanged;
        }

        private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(e.After, 0, e.After.Length)));
        }

        /// <summary>
        /// 从指定行开始向上查找最近的合法 header。
        /// 返回 header 字符串（如 "skill:"），若到达文件顶部仍未找到则返回 null。
        /// </summary>
        private string FindHeaderAbove(ITextSnapshot snapshot, int fromLineNumber)
        {
            for (int i = fromLineNumber; i >= 0; i--)
            {
                var line = snapshot.GetLineFromLineNumber(i);
                string lineText = line.GetText();

                // 跳过空行和注释行
                if (string.IsNullOrWhiteSpace(lineText) || _commentRegex.IsMatch(lineText.TrimStart()))
                    continue;

                var headerMatch = _headerRegex.Match(lineText);
                if (headerMatch.Success)
                    return headerMatch.Value;
            }
            return null;
        }

		/// <summary>
		/// 从当前关键字位置向前扫描，找到上一个合法的 .关键字。
		/// 合法点号要求：点号前不是数字。
		/// 返回上一个关键字文本，例如 ".target"。
		/// </summary>
		private string FindPreviousDotKeyword(ITextSnapshot snapshot, int currentKeywordStart)
		{
			int pos = currentKeywordStart - 1;

			while (pos >= 0)
			{
				char ch = snapshot[pos];

				if (ch == '.')
				{
					// 点号前后都不能是数字
					bool prevIsDigit = pos > 0 && char.IsDigit(snapshot[pos - 1]);
					bool nextIsDigit = pos + 1 < currentKeywordStart && char.IsDigit(snapshot[pos + 1]);

					if (prevIsDigit || nextIsDigit)
					{
						pos--;
						continue;
					}

					int keywordStart = pos;
					int keywordEnd = pos + 1;

					while (keywordEnd < currentKeywordStart)
					{
						char c = snapshot[keywordEnd];

						// .关键字结束条件：空白、制表符、换行等
						if (char.IsWhiteSpace(c))
							break;

						// 如果中途又遇到点，说明格式不正常，停在这里
						if (c == '.' && keywordEnd != keywordStart)
							break;

						keywordEnd++;
					}

					if (keywordEnd > keywordStart + 1)
						return snapshot.GetText(keywordStart, keywordEnd - keywordStart);

					return ".";
				}

				pos--;
			}

			return null;
		}

		public IEnumerable<ITagSpan<IErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0) yield break;

            foreach (var span in spans)
            {
                ITextSnapshot snapshot = span.Snapshot;
                int startLine = span.Start.GetContainingLine().LineNumber;
                int endLine = span.End.GetContainingLine().LineNumber;

                for (int i = startLine; i <= endLine; i++)
                {
                    var line = snapshot.GetLineFromLineNumber(i);
                    string lineText = line.GetText();

                    if (string.IsNullOrWhiteSpace(lineText) || _commentRegex.IsMatch(lineText.TrimStart()))
                        continue;

                    var stringSpans = _stringRegex.Matches(lineText)
                        .Cast<Match>()
                        .Select(m => new Span(m.Index, m.Length))
                        .ToList();

                    // 1. 先看当前行是否有 header
                    string currentHeader = null;
                    bool currentLineIsHeader = false;
                    var headerMatch = _headerRegex.Match(lineText);
                    if (headerMatch.Success)
                    {
                        currentHeader = headerMatch.Value;
                        currentLineIsHeader = true;
                    }

                    // 2. 当前行是 header 行：校验 header 合法性，然后继续（同一行可能还有关键字）
                    if (currentLineIsHeader)
                    {
                        if (!DarkestInfoData.AllHeaders.Contains(currentHeader))
                        {
                            yield return new TagSpan<IErrorTag>(
                                new SnapshotSpan(line.Start, line.Length),
                                new ErrorTag(PredefinedErrorTypeNames.SyntaxError, $"未知的 Header: {currentHeader}"));
                            // header 本身不合法，跳过本行关键字检查
                            continue;
                        }
                        // header 合法，后面关键字校验正常走
                    }
                    else
                    {
                        // 3. 当前行没有 header，向上查找最近的合法 header
                        currentHeader = FindHeaderAbove(snapshot, i - 1);

                        if (currentHeader == null)
                        {
                            // 追溯到文件顶部也没有找到任何 header
                            // 只在有关键字的行上报错，避免对纯数值/文本行误报
                            bool hasKeyword = _keywordRegex.Matches(lineText)
                                .Cast<Match>()
                                .Any(m => !stringSpans.Any(s => s.Contains(m.Index))
                                          && !(m.Index > 0 && char.IsDigit(lineText[m.Index - 1])));

                            if (hasKeyword)
                            {
                                yield return new TagSpan<IErrorTag>(
                                    new SnapshotSpan(line.Start, line.Length),
                                    new ErrorTag(PredefinedErrorTypeNames.SyntaxError, "缺少 Header：该关键字前没有任何合法的 Header 定义"));
                            }
                            continue;
                        }
                        // 找到了上方的 header，用它来校验本行关键字
                        if (!_keywordRegex.Match(lineText).Success)
                        {
                            // 本行没有任何关键字，报错非法内容
                            yield return new TagSpan<IErrorTag>(
                                new SnapshotSpan(line.Start, line.Length),
                                new ErrorTag(PredefinedErrorTypeNames.SyntaxError, $"错误内容"));
                        }
                    }

                    // 4. 检查行内的每一个关键字
                    foreach (Match match in _keywordRegex.Matches(lineText))
                    {
                        if (stringSpans.Any(s => s.Contains(match.Index))) continue;
                        if (match.Index > 0 && char.IsDigit(lineText[match.Index - 1])) continue;

                        string keyword = match.Value;
                        bool isValid = false;
                        string errorMsg = $"无效的关键字: {keyword}";

                        bool isDefinedInCurrentHeader = currentHeader != null &&
                                                       DarkestInfoData.InfoContextMap.TryGetValue(currentHeader, out var allowedList) &&
                                                       allowedList.Contains(keyword);

                        bool isKnownStaticKeyword = DarkestInfoData.InfoContextMap.Values.Any(list => list.Contains(keyword));

                        if (isDefinedInCurrentHeader)
                        {
                            isValid = true;
                        }
                        else if (isKnownStaticKeyword)
                        {
                            errorMsg = $"关键字 '{keyword}' 不属于 Header '{currentHeader}'。";
                            isValid = false;
                        }
                        else if (keyword.EndsWith("_effects"))
                        {
                            if (currentHeader != null && _allowedEffectsHeaders.Contains(currentHeader))
                            {
								// 匹配形如 .critxxx_effects 的非法关键字
								var matchDynamic = Regex.Match(keyword, @"^\.(?<body>[^\s.]+)_effects$");
                                if (matchDynamic.Success)
                                {
                                    string body = matchDynamic.Groups["body"].Value;

                                    // 找到技能自带前缀前缀
                                    string matchedPrefix = _allowedEffectsHeaders
                                        .Where(key => DarkestInfoData.InfoContextMap.TryGetValue(key, out _))
                                        .SelectMany(key => DarkestInfoData.InfoContextMap[key])
                                        .OrderByDescending(p => p.Length)
                                        .FirstOrDefault(p =>
										{
											string prefix = p.StartsWith(".") ? p.Substring(1) : p; // 去除开头的.
											return body.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
										});

									if (matchedPrefix == null)
                                    {
										// 特判：
										// 如果当前 .mmm_effects 前一个合法 .关键字是 .target，
										// 且 mmm/body 中包含至少一个数字，则报错。
										int currentKeywordStart = line.Start.Position + match.Index;
										string previousKeyword = FindPreviousDotKeyword(snapshot, currentKeywordStart);

										if (body.Any(char.IsDigit) &&
											string.Equals(previousKeyword, ".target", StringComparison.OrdinalIgnoreCase))
										{
											errorMsg = $"模式差分effect '{keyword}' 紧跟 .target 时，模式名中不能包含数字，否则可能导致识别错误。建议在这两者之间插入.valid_modes或其他内容";
											isValid = false;
										}
                                        else
										    isValid = true;
                                    }
                                    else
                                    {
                                        errorMsg = $"模式差分effect不能以技能本身带有的相关关键字（如.crit）作为开头，否则将导致红钩识别错误，请更改模式名";
                                        isValid = false;
                                    }
                                }
                            }
                            else
                            {
                                errorMsg = $"动态效果关键字 '{keyword}' 只能用于技能类 Header (如 skill:)。";
                                isValid = false;
                            }
                        }
                        else
                        {
                            isValid = false;
                        }

                        if (!isValid)
                        {
                            var errorSpan = new SnapshotSpan(snapshot, line.Start + match.Index, match.Length);
                            yield return new TagSpan<IErrorTag>(errorSpan, new ErrorTag(PredefinedErrorTypeNames.SyntaxError, errorMsg));
                        }
                    }
                }
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
    }

    [Export(typeof(ITaggerProvider))]
    [ContentType("darkest-info")]
    [TagType(typeof(IErrorTag))]
    internal class InfoErrorTaggerProvider : ITaggerProvider
    {
        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            return buffer.Properties.GetOrCreateSingletonProperty(() => new InfoErrorTagger(buffer)) as ITagger<T>;
        }
    }
}