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
    internal class EffectErrorTagger : ITagger<IErrorTag>
    {
        private readonly ITextBuffer _buffer;

        private readonly Regex _keywordRegex = new Regex(@"\.[a-zA-Z_][a-zA-Z0-9_]*", RegexOptions.Compiled);
        private readonly Regex _stringRegex = new Regex(@"""([^""]*)""", RegexOptions.Compiled);
        private readonly Regex _nextParamRegex = new Regex(@"^\s+(?:""([^""]*)""|([a-zA-Z0-9_]+))", RegexOptions.Compiled);

        internal EffectErrorTagger(ITextBuffer buffer)
        {
            _buffer = buffer;
            _buffer.Changed += OnBufferChanged;
        }

		private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
		{
			var snapshot = e.After;
			if (e.Changes.Count == 0)
				return;

			int start = e.Changes.Min(c => c.NewSpan.Start);
			int end = e.Changes.Max(c => c.NewSpan.End);

			ITextSnapshotLine startLine = snapshot.GetLineFromPosition(Math.Min(start, snapshot.Length));
			ITextSnapshotLine endLine = snapshot.GetLineFromPosition(Math.Min(end, snapshot.Length));

			bool needsFullTailRefresh = e.Changes.Any(c =>
			{
				string newText = c.NewText ?? "";
				string oldText = c.OldText ?? "";

				// 换行变化可能影响块结构
				if (newText.Contains("\n") || newText.Contains("\r") ||
					oldText.Contains("\n") || oldText.Contains("\r"))
					return true;

				// 当前修改涉及 effect: / 冒号结构，才刷新后续
				string changedLineText = startLine.GetText();
				return changedLineText.Contains("effect:") || changedLineText.Contains(":");
			});

			if (needsFullTailRefresh)
			{
				TagsChanged?.Invoke(
					this,
					new SnapshotSpanEventArgs(
						new SnapshotSpan(snapshot, startLine.Start, snapshot.Length - startLine.Start)));
			}
			else
			{
				int spanStart = startLine.Start.Position;
				int spanEnd = endLine.End.Position;

				TagsChanged?.Invoke(
					this,
					new SnapshotSpanEventArgs(
						new SnapshotSpan(snapshot, spanStart, spanEnd - spanStart)));
			}
		}

		/// <summary>
		/// 向上寻找该行是否属于一个 effect 块
		/// </summary>
		private bool IsInsideEffectBlock(ITextSnapshot snapshot, int fromLineNumber)
        {
            // 向上遍历，直到找到一个包含冒号的行
            for (int i = fromLineNumber; i >= 0; i--)
            {
                var line = snapshot.GetLineFromLineNumber(i);
                string text = line.GetText().Trim();

                // 跳过空行和注释
                if (string.IsNullOrWhiteSpace(text) || text.StartsWith("//"))
                    continue;

                // 检查这行是否有冒号（作为 Header）
                // 排除字符串内的冒号
                int colonIndex = GetFirstLogicalColon(line.GetText());

                if (colonIndex != -1)
                {
                    string header = line.GetText().Substring(0, colonIndex + 1).Trim();
                    // 如果是 effect: 则是合法的块开始
                    return header.Equals("effect:", StringComparison.OrdinalIgnoreCase);
                }

                // 如果这行没有冒号，且不是空行/注释，说明它本身也是属性行，继续向上找 Header
            }
            return false;
        }

        private int GetFirstLogicalColon(string lineText)
        {
            var stringMatches = _stringRegex.Matches(lineText).Cast<Match>();
            var stringSpans = stringMatches.Select(m => new Span(m.Index, m.Length)).ToList();

            for (int c = 0; c < lineText.Length; c++)
            {
                if (lineText[c] == ':' && !stringSpans.Any(s => s.Contains(c)))
                {
                    return c;
                }
            }
            return -1;
        }

		/// <summary>
		/// name 长度检查辅助函数
		/// </summary>
		private bool TryGetNameValueInfo(
	string lineText,
	int keywordStart,
	int keywordLength,
	List<Span> stringSpans,
	out int valueLength,
	out int valueStart,
	out int valueSpanLength,
	out bool isQuoted) // 新增
		{
			valueLength = 0;
			valueStart = -1;
			valueSpanLength = 0;
			isQuoted = false;

			int pos = keywordStart + keywordLength;

			// 跳过空白
			while (pos < lineText.Length && char.IsWhiteSpace(lineText[pos]))
				pos++;

			if (pos >= lineText.Length)
				return false;

			if (lineText[pos] == '"')
			{
				isQuoted = true;
				int quoteStart = pos;
				int quoteEnd = lineText.IndexOf('"', quoteStart + 1);
				if (quoteEnd < 0)
					quoteEnd = lineText.Length;

				valueStart = quoteStart + 1;
				valueSpanLength = Math.Max(0, quoteEnd - valueStart);
				valueLength = valueSpanLength;
				return true;
			}

			// 无引号情况
			int end = lineText.Length;
			int commentIndex = lineText.IndexOf("//", pos, StringComparison.Ordinal);
			if (commentIndex >= 0)
				end = commentIndex;

			foreach (Match nextMatch in _keywordRegex.Matches(lineText))
			{
				if (nextMatch.Index <= pos) continue;
				if (stringSpans.Any(s => s.Contains(nextMatch.Index))) continue;
				if (nextMatch.Index > 0 && char.IsDigit(lineText[nextMatch.Index - 1])) continue;

				end = Math.Min(end, nextMatch.Index);
				break;
			}

			string rawValue = lineText.Substring(pos, end - pos);

			int leadingSpaces = 0;
			while (leadingSpaces < rawValue.Length && char.IsWhiteSpace(rawValue[leadingSpaces]))
				leadingSpaces++;

			int trailingSpaces = rawValue.Length - 1;
			while (trailingSpaces >= leadingSpaces && char.IsWhiteSpace(rawValue[trailingSpaces]))
				trailingSpaces--;

			if (trailingSpaces < leadingSpaces)
				return false;

			valueStart = pos + leadingSpaces;
			valueSpanLength = trailingSpaces - leadingSpaces + 1;

			string trimmedValue = lineText.Substring(valueStart, valueSpanLength);

			// 无引号情况长度排除空白
			valueLength = trimmedValue.Count(c => !char.IsWhiteSpace(c));

			return true;
		}

		/// <summary>
		/// buff_ids/set_monster_class_ids 长度检查辅助函数
		/// </summary>
		private bool IsKeywordStartAt(string lineText, int pos)
		{
			if (pos < 0 || pos >= lineText.Length)
				return false;

			if (lineText[pos] != '.')
				return false;

			bool prevIsDigit = pos > 0 && char.IsDigit(lineText[pos - 1]);
			bool nextIsDigit = pos + 1 < lineText.Length && char.IsDigit(lineText[pos + 1]);

			if (prevIsDigit || nextIsDigit)
				return false;

			return pos + 1 < lineText.Length &&
				   (char.IsLetter(lineText[pos + 1]) || lineText[pos + 1] == '_');
		}

		public IEnumerable<ITagSpan<IErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            foreach (var span in spans)
            {
                var snapshot = span.Snapshot;
                int startLine = snapshot.GetLineNumberFromPosition(span.Start);
                int endLine = snapshot.GetLineNumberFromPosition(span.End);

                for (int i = startLine; i <= endLine; i++)
                {
                    var line = snapshot.GetLineFromLineNumber(i);
                    string lineText = line.GetText();

                    // 1. 完全忽略空行和纯注释行（不产生任何错误）
                    if (string.IsNullOrWhiteSpace(lineText) || lineText.TrimStart().StartsWith("//"))
                        continue;

                    int firstColonIndex = GetFirstLogicalColon(lineText);

                    // 2. 结构校验
                    if (firstColonIndex == -1)
                    {
                        // 如果本行没有冒号，也不是空行，那么它必须位于某个 effect: 块下方
                        if (!IsInsideEffectBlock(snapshot, i - 1))
                        {
                            yield return new TagSpan<IErrorTag>(
                                new SnapshotSpan(line.Start, line.Length),
                                new ErrorTag(PredefinedErrorTypeNames.SyntaxError, "此行不属于任何 'effect:'")
                            );
                        }
                        else
                        {
                            if (_keywordRegex.Match(lineText).Success)
                                yield return new TagSpan<IErrorTag>(
                                    new SnapshotSpan(line.Start, line.Length),
                                    new ErrorTag(PredefinedErrorTypeNames.Warning, "建议单条effect不在内部换行，如有需求请尽量用分行写法")
                                );
                            else
                                yield return new TagSpan<IErrorTag>(
                                    new SnapshotSpan(line.Start, line.Length),
                                    new ErrorTag(PredefinedErrorTypeNames.SyntaxError, "错误内容")
                                );
                        }
                    }
                    else
                    {
                        // 如果本行有冒号，它必须是 "effect:"
                        string header = lineText.Substring(0, firstColonIndex + 1).Trim();
                        if (!header.Equals("effect:", StringComparison.OrdinalIgnoreCase))
                        {
                            yield return new TagSpan<IErrorTag>(
                                new SnapshotSpan(line.Start, firstColonIndex + 1),
                                new ErrorTag(PredefinedErrorTypeNames.SyntaxError, $"无效的 Header '{header}'，Effect 文件需使用 'effect:'")
                            );
                        }
                    }

                    // 3. 关键字和参数校验
                    var stringMatches = _stringRegex.Matches(lineText).Cast<Match>().ToList();
                    var stringSpans = stringMatches.Select(m => new Span(m.Index, m.Length)).ToList();

                    foreach (Match match in _keywordRegex.Matches(lineText))
                    {
                        if (stringSpans.Any(s => s.Contains(match.Index))) continue;
                        if (match.Index > 0 && char.IsDigit(lineText[match.Index - 1])) continue;

                        string keyword = match.Value;
                        if (!DarkestEffectsData.AllKeywords.Contains(keyword))
                        {
                            yield return new TagSpan<IErrorTag>(
                                new SnapshotSpan(snapshot, line.Start + match.Index, match.Length),
                                new ErrorTag(PredefinedErrorTypeNames.SyntaxError, $"无效关键字: {keyword}")
                            );
                            continue;
                        }

						if (keyword == ".name")
						{
							if (TryGetNameValueInfo(
								lineText,
								match.Index,
								match.Length,
								stringSpans,
								out int nameLength,
								out int nameValueStart,
								out int nameValueSpanLength,
								out bool isQuoted)) // 新增返回参数表示是否带引号
							{
								string nameValue = lineText.Substring(nameValueStart, nameValueSpanLength);

								if (!isQuoted && nameValue.Any(c => char.IsWhiteSpace(c)))
								{
									// 无引号且出现空格/制表符 → 报错
									yield return new TagSpan<IErrorTag>(
										new SnapshotSpan(snapshot, line.Start + nameValueStart, nameValueSpanLength),
										new ErrorTag(
											PredefinedErrorTypeNames.SyntaxError,
											$".name 参数无引号时不能包含空白字符: '{nameValue}'"));
								}
								else
								{
									// 长度判断
									int actualLength = nameValue.Count(c => !char.IsWhiteSpace(c));
									if (actualLength == 64)
									{
										yield return new TagSpan<IErrorTag>(
											new SnapshotSpan(snapshot, line.Start + nameValueStart, nameValueSpanLength),
											new ErrorTag(
												PredefinedErrorTypeNames.Warning,
												$".name 的名称长度已经达到 64 个字符，建议缩短"));
									}
									else if (actualLength > 64)
									{
										yield return new TagSpan<IErrorTag>(
											new SnapshotSpan(snapshot, line.Start + nameValueStart, nameValueSpanLength),
											new ErrorTag(
												PredefinedErrorTypeNames.SyntaxError,
												$".name 的名称长度不能超过 64 个字符，当前长度为 {actualLength}"));
									}
								}
							}
						}

						if (keyword == ".buff_ids" || keyword == ".set_monster_class_ids")
						{
							int pos = match.Index + match.Length;
							var args = new List<(int start, int length, string value)>();

							while (pos < lineText.Length)
							{
								// 跳过空白
								while (pos < lineText.Length && char.IsWhiteSpace(lineText[pos]))
									pos++;

								if (pos >= lineText.Length)
									break;

								// 遇到下一个 .keyword，说明当前关键字的参数结束
								if (IsKeywordStartAt(lineText, pos))
									break;

								int argStart = pos;
								int argLength;
								string argValue;

								if (lineText[pos] == '"')
								{
									int quoteStart = pos;
									int quoteEnd = lineText.IndexOf('"', quoteStart + 1);

									if (quoteEnd < 0)
										quoteEnd = lineText.Length;

									argStart = quoteStart + 1;
									argLength = Math.Max(0, quoteEnd - argStart);
									argValue = lineText.Substring(argStart, argLength);

									if (argValue.Any(char.IsWhiteSpace))
									{
										if (keyword == ".buff_ids")
											yield return new TagSpan<IErrorTag>(
												new SnapshotSpan(snapshot, line.Start + argStart, argLength),
												new ErrorTag(
													PredefinedErrorTypeNames.Warning,
													$"{keyword} 引号内部参数强烈不建议包含空格或制表符: '{argValue}'"));
										else
											yield return new TagSpan<IErrorTag>(
												new SnapshotSpan(snapshot, line.Start + argStart, argLength),
												new ErrorTag(
													PredefinedErrorTypeNames.SyntaxError,
													$"{keyword} 引号内部参数不能包含空格或制表符: '{argValue}'"));
									}

									pos = quoteEnd < lineText.Length ? quoteEnd + 1 : lineText.Length;
								}
								else
								{
									int argEnd = pos;
									while (argEnd < lineText.Length && !char.IsWhiteSpace(lineText[argEnd]))
										argEnd++;

									argLength = argEnd - pos;
									argValue = lineText.Substring(pos, argLength);
									pos = argEnd;
								}

								args.Add((argStart, argLength, argValue));

								if (args.Count >= 9)
									break;
							}

							foreach (var (start, length, value) in args)
							{
								int actualLength = value.Count(c => !char.IsWhiteSpace(c) && c != '"');

								if (actualLength == 64)
								{
									yield return new TagSpan<IErrorTag>(
										new SnapshotSpan(snapshot, line.Start + start, length),
										new ErrorTag(
											PredefinedErrorTypeNames.Warning,
											$"{keyword} 参数长度已经达到 64 个字符，建议缩短"));
								}
								else if (actualLength > 64)
								{
									yield return new TagSpan<IErrorTag>(
										new SnapshotSpan(snapshot, line.Start + start, length),
										new ErrorTag(
											PredefinedErrorTypeNames.SyntaxError,
											$"{keyword} 参数长度不能超过 64 个字符，当前长度为 {actualLength}"));
								}
							}

							if (args.Count > 8)
							{
								if (keyword == ".buff_ids")
									yield return new TagSpan<IErrorTag>(
										new SnapshotSpan(snapshot, line.Start + match.Index, match.Length),
										new ErrorTag(
											PredefinedErrorTypeNames.SyntaxError,
											$"{keyword} 参数数量不能超过 8 个，当前数量为 {args.Count}，建议采用分行写法"));
								else
									yield return new TagSpan<IErrorTag>(
										new SnapshotSpan(snapshot, line.Start + match.Index, match.Length),
										new ErrorTag(
											PredefinedErrorTypeNames.SyntaxError,
											$"{keyword} 参数数量不能超过 8 个，当前数量为 {args.Count}"));
							}
							else if (args.Count == 8)
							{
								yield return new TagSpan<IErrorTag>(
									new SnapshotSpan(snapshot, line.Start + match.Index, match.Length),
									new ErrorTag(
										PredefinedErrorTypeNames.Warning,
										$"{keyword} 参数数量已达到 8 个，建议不要再增加参数"));
							}
						}

						if (keyword == ".spawn_target_actor_base_class_id")
						{
							int keywordIndex = match.Index;

							// 从 .spawn_target_actor_base_class_id 向前扫描到第一个 effect:
							int scanPos = keywordIndex - 1;
							bool targetFound = false;

							while (scanPos >= 0)
							{
								// 找到 effect: 就停止
								if (scanPos + 7 <= lineText.Length &&
	                                string.Compare(lineText, scanPos, "effect:", 0, 7, StringComparison.OrdinalIgnoreCase) == 0)
									break;

								// 检查 .target 出现
								if (scanPos + 7 <= lineText.Length && 
									string.Compare(lineText, scanPos, ".target", 0, 7, StringComparison.Ordinal) == 0)
								{
									targetFound = true;
									break;
								}

								scanPos--;
							}

							if (targetFound)
							{
								string errorMsg = ".spawn_target_actor_base_class_id 必须写在 .target 前，否则 spawn 定向不能生效";

								var errorSpan = new SnapshotSpan(line.Snapshot, line.Start + match.Index, match.Length);
								yield return new TagSpan<IErrorTag>(errorSpan, new ErrorTag(PredefinedErrorTypeNames.SyntaxError, errorMsg));

								continue; // 这条关键字已报错，跳过后续检查
							}
						}

						if (DarkestEffectsData.KeywordToValuesMap.TryGetValue(keyword, out List<string> validValues))
                        {
                            var remainingText = lineText.Substring(match.Index + match.Length);
                            var paramMatch = _nextParamRegex.Match(remainingText);
                            if (paramMatch.Success)
                            {
                                string valInQuote = paramMatch.Groups[1].Value;
                                string valPlain = paramMatch.Groups[2].Value;
                                bool isQuoted = paramMatch.Groups[1].Success || paramMatch.Value.Contains("\"\"");
                                string actualValue = isQuoted ? valInQuote : valPlain;

                                bool isParamValid = validValues.Contains(actualValue);

                                if (!isParamValid)
                                {
                                    int valOffset = isQuoted ? paramMatch.Value.IndexOf('"') : paramMatch.Value.IndexOf(actualValue);
                                    int errorStart = line.Start + match.Index + match.Length + paramMatch.Index + valOffset;
                                    int errorLen = isQuoted ? (paramMatch.Groups[1].Length + 2) : actualValue.Length;

                                    yield return new TagSpan<IErrorTag>(
                                        new SnapshotSpan(snapshot, errorStart, errorLen),
                                        new ErrorTag(PredefinedErrorTypeNames.SyntaxError, $"值 '{actualValue}' 对关键字 '{keyword}' 无效")
                                    );
                                }
                                else if (isQuoted && DarkestEffectsData.NumBoolValues.Contains(actualValue))
                                {
                                    int valOffset = paramMatch.Value.IndexOf('"');
                                    int errorStart = line.Start + match.Index + match.Length + paramMatch.Index + valOffset;
                                    int errorLen = isQuoted ? (paramMatch.Groups[1].Length + 2) : actualValue.Length;

                                    yield return new TagSpan<IErrorTag>(
                                        new SnapshotSpan(snapshot, errorStart, errorLen),
                                        new ErrorTag(PredefinedErrorTypeNames.SyntaxError, $"值 '{actualValue}' 不应带引号")
                                    );
                                }
                            }
                        }
                    }
                }
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
    }

    [Export(typeof(ITaggerProvider))]
    [ContentType("darkest-effect")]
    [TagType(typeof(IErrorTag))]
    internal class EffectErrorTaggerProvider : ITaggerProvider
    {
        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            return buffer.Properties.GetOrCreateSingletonProperty(() => new EffectErrorTagger(buffer)) as ITagger<T>;
        }
    }
}