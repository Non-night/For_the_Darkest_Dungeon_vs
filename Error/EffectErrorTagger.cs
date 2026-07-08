using For_the_Darkest_Dungeon.DefinitionDarkest;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Differencing;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text.RegularExpressions;

using RegexMatch = System.Text.RegularExpressions.Match;
using Regex = System.Text.RegularExpressions.Regex;

namespace For_the_Darkest_Dungeon.Error
{
    internal class EffectErrorTagger : ITagger<IErrorTag>
    {
        private readonly ITextBuffer _buffer;

        private readonly Regex _keywordRegex = new Regex(@"\.[a-zA-Z_][a-zA-Z0-9_]*", RegexOptions.Compiled);
        private readonly Regex _stringRegex = new Regex(@"""([^""]*)""", RegexOptions.Compiled);
        private readonly Regex _nextParamRegex = new Regex(@"^\s+(?:""([^""]*)""|([a-zA-Z0-9_]+))", RegexOptions.Compiled);

		private static readonly string[] DotKeywordsToCheck = new[]
		{
			".dotBleed",
			".dotPoison",
			".dotStress",
			".dotHpHeal",
			".dotShuffle"
		};

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

		/// <summary>
		/// 获取一行中 // 注释开始的位置。
		/// 注意：这里不区分是否位于引号内，因为 // 具有最高注释优先级。
		/// 若不存在注释则返回 -1。
		/// </summary>
		private int GetCommentStartIndex(string lineText)
		{
			return lineText.IndexOf("//", StringComparison.Ordinal);
		}

		/// <summary>
		/// 将单行文本裁剪为真正参与语法分析的代码部分。
		/// // 之后直到行尾都视为注释，不参与任何关键字扫描。
		/// </summary>
		private string TrimCommentFromLine(string lineText)
		{
			int commentIndex = GetCommentStartIndex(lineText);
			return commentIndex >= 0 ? lineText.Substring(0, commentIndex) : lineText;
		}


		/// <summary>
		/// 判断当前行是否出现了“行尾注释”。
		/// 只有 // 前面存在实际代码内容时，才视为行尾注释；
		/// 若 // 前面只有空白，则仍视为普通注释行，不算行尾注释。
		/// </summary>
		private bool TryGetInlineCommentStart(string lineText, out int commentIndex)
		{
			commentIndex = GetCommentStartIndex(lineText);
			if (commentIndex < 0)
				return false;

			string textBeforeComment = lineText.Substring(0, commentIndex);
			return !string.IsNullOrWhiteSpace(textBeforeComment);
		}
		/// <summary>
		/// 获取包含指定行号的整个 effect 块文本范围。
		/// 向前会一直扫描到 effect: 或文件开头，向后会一直扫描到下一个 effect: 或文件末尾。
		/// 同时会移除每一行中 // 之后的注释内容，以匹配游戏引擎的截断规则。
		/// </summary>
		private bool TryGetEffectBlockCodeRange(
			ITextSnapshot snapshot,
			int lineNumber,
			out int blockStart,
			out int blockEnd,
			out string blockCodeText)
		{
			blockStart = 0;
			blockEnd = 0;
			blockCodeText = string.Empty;

			int effectStartLine = -1;
			for (int i = lineNumber; i >= 0; i--)
			{
				ITextSnapshotLine currentLine = snapshot.GetLineFromLineNumber(i);
				string currentCodeText = TrimCommentFromLine(currentLine.GetText());
				int colonIndex = GetFirstLogicalColon(currentCodeText);

				if (colonIndex != -1)
				{
					string header = currentCodeText.Substring(0, colonIndex + 1).Trim();
					if (header.Equals("effect:", StringComparison.OrdinalIgnoreCase))
					{
						effectStartLine = i;
						break;
					}
				}
			}

			if (effectStartLine == -1)
				return false;

			int effectEndLine = snapshot.LineCount - 1;
			for (int i = effectStartLine + 1; i < snapshot.LineCount; i++)
			{
				ITextSnapshotLine currentLine = snapshot.GetLineFromLineNumber(i);
				string currentCodeText = TrimCommentFromLine(currentLine.GetText());
				int colonIndex = GetFirstLogicalColon(currentCodeText);

				if (colonIndex != -1)
				{
					string header = currentCodeText.Substring(0, colonIndex + 1).Trim();
					if (header.Equals("effect:", StringComparison.OrdinalIgnoreCase))
					{
						effectEndLine = i - 1;
						break;
					}
				}
			}

			var parts = new List<string>();
			for (int i = effectStartLine; i <= effectEndLine; i++)
			{
				ITextSnapshotLine currentLine = snapshot.GetLineFromLineNumber(i);
				parts.Add(TrimCommentFromLine(currentLine.GetText()));
			}

			blockStart = snapshot.GetLineFromLineNumber(effectStartLine).Start.Position;
			blockEnd = snapshot.GetLineFromLineNumber(effectEndLine).End.Position;
			blockCodeText = string.Join("\n", parts);
			return true;
		}
        private int GetFirstLogicalColon(string lineText)
        {
            var stringMatches = _stringRegex.Matches(lineText).Cast<RegexMatch>();
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

			foreach (RegexMatch nextMatch in _keywordRegex.Matches(lineText))
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

		/// <summary>
		/// 检查 codeText 中是否存在中文字符或中文标点。
		///
		/// 重要规则：
		/// - 传进来的必须是 codeText，也就是 // 前面的内容；
		/// - 因此 // 后面的注释内容不会被检查；
		/// - 会把连续中文字符合并成一个错误 Span，避免每个字都报一个错误；
		/// - 支持常用 CJK 字符、扩展区生僻字、兼容汉字、常见中文标点和全角中文标点。
		/// </summary>
		private IEnumerable<ITagSpan<IErrorTag>> CreateChineseCharacterErrors(
			ITextSnapshot snapshot,
			ITextSnapshotLine line,
			string codeText)
		{
			int start = -1;
			int length = 0;

			for (int i = 0; i < codeText.Length;)
			{
				if (IsChineseCharacterOrPunctuation(codeText, i, out int charLength))
				{
					if (start < 0)
					{
						start = i;
						length = charLength;
					}
					else
					{
						length += charLength;
					}

					i += charLength;
					continue;
				}

				if (start >= 0)
				{
					string badText = codeText.Substring(start, length);

					yield return new TagSpan<IErrorTag>(
						new SnapshotSpan(
							snapshot,
							line.Start.Position + start,
							length),
						new ErrorTag(
							PredefinedErrorTypeNames.SyntaxError,
							$"不允许出现中文字符或中文标点: {badText}"));

					start = -1;
					length = 0;
				}

				i++;
			}

			if (start >= 0)
			{
				string badText = codeText.Substring(start, length);

				yield return new TagSpan<IErrorTag>(
					new SnapshotSpan(
						snapshot,
						line.Start.Position + start,
						length),
					new ErrorTag(
						PredefinedErrorTypeNames.SyntaxError,
						$"不允许出现中文字符或中文标点: {badText}"));
			}
		}

		/// <summary>
		/// 判断指定位置是否是中文字符或中文标点。
		///
		/// 说明：
		/// - BMP 内的常用汉字、扩展 A、兼容汉字直接通过 char 判断；
		/// - 扩展 B/C/D/E/F/G/H 等生僻字位于 Unicode 辅助平面，需要处理代理对；
		/// - 中文标点覆盖 CJK Symbols and Punctuation、Vertical Forms、CJK Compatibility Forms、
		///   以及常见全角标点区间。
		/// </summary>
		private bool IsChineseCharacterOrPunctuation(string text, int index, out int charLength)
		{
			charLength = 1;

			if (index < 0 || index >= text.Length)
				return false;

			int codePoint;

			if (char.IsHighSurrogate(text[index]) &&
				index + 1 < text.Length &&
				char.IsLowSurrogate(text[index + 1]))
			{
				codePoint = char.ConvertToUtf32(text[index], text[index + 1]);
				charLength = 2;
			}
			else
			{
				codePoint = text[index];
			}

			// 常用汉字与 BMP 内扩展区
			if ((codePoint >= 0x3400 && codePoint <= 0x4DBF) ||   // CJK Extension A
				(codePoint >= 0x4E00 && codePoint <= 0x9FFF) ||   // CJK Unified Ideographs
				(codePoint >= 0xF900 && codePoint <= 0xFAFF))     // CJK Compatibility Ideographs
			{
				return true;
			}

			// 生僻字扩展区，位于 Unicode 辅助平面，需要代理对。
			if ((codePoint >= 0x20000 && codePoint <= 0x2A6DF) || // Extension B
				(codePoint >= 0x2A700 && codePoint <= 0x2B73F) || // Extension C
				(codePoint >= 0x2B740 && codePoint <= 0x2B81F) || // Extension D
				(codePoint >= 0x2B820 && codePoint <= 0x2CEAF) || // Extension E
				(codePoint >= 0x2CEB0 && codePoint <= 0x2EBEF) || // Extension F
				(codePoint >= 0x30000 && codePoint <= 0x3134F) || // Extension G
				(codePoint >= 0x31350 && codePoint <= 0x323AF))   // Extension H
			{
				return true;
			}

			// 中文标点、书名号、顿号、中文括号、全角空格等。
			if ((codePoint >= 0x3000 && codePoint <= 0x303F) ||   // CJK Symbols and Punctuation
				(codePoint >= 0xFE10 && codePoint <= 0xFE1F) ||   // Vertical Forms
				(codePoint >= 0xFE30 && codePoint <= 0xFE4F))     // CJK Compatibility Forms
			{
				return true;
			}

			// 常见全角中文标点。
			// 不直接包含整个 FF00-FFEF，避免把全角英文字母/数字也全部算作中文。
			if ((codePoint >= 0xFF01 && codePoint <= 0xFF0F) ||   // ！＂＃＄％＆＇（）＊＋，－．／
				(codePoint >= 0xFF1A && codePoint <= 0xFF20) ||   // ：；＜＝＞？＠
				(codePoint >= 0xFF3B && codePoint <= 0xFF40) ||   // ［＼］＾＿｀
				(codePoint >= 0xFF5B && codePoint <= 0xFF65))     // ｛｜｝～｡､･｢｣
			{
				return true;
			}

			return false;
		}

		/// <summary>
		/// 从当前关键字之后，查找同一行 codeText 中是否还存在某个目标关键字。
		///
		/// 用途示例：
		/// .heal 1 .healstress 1
		/// 当当前关键字是 .heal 时，向后查找是否存在 .healstress。
		///
		/// 注意：
		/// 1. 只检查 currentKeywordIndex 之后的关键字；
		/// 2. 忽略字符串内部的关键字；
		/// 3. 忽略类似 1.xxx 这种小数/数字伪关键字；
		/// 4. 使用 Match.Value 精确比较，因此 .heal_percent 不会被当成 .heal。
		/// </summary>
		private bool HasLaterKeyword(
			string codeText,
			int currentKeywordIndex,
			string targetKeyword,
			List<Span> stringSpans)
		{
			foreach (RegexMatch laterMatch in _keywordRegex.Matches(codeText))
			{
				// 只看当前关键字之后的内容。
				if (laterMatch.Index <= currentKeywordIndex)
					continue;

				// 必须精确等于目标关键字。
				// 例如 targetKeyword 是 ".healstress"，
				// 那么 ".healstress_percent" 不会被误判。
				if (!string.Equals(laterMatch.Value, targetKeyword, StringComparison.Ordinal))
					continue;

				// 忽略字符串内部的 .xxx。
				if (stringSpans.Any(s => s.Contains(laterMatch.Index)))
					continue;

				// 忽略数字小数等情况，例如 1.xxx。
				if (laterMatch.Index > 0 && char.IsDigit(codeText[laterMatch.Index - 1]))
					continue;

				return true;
			}

			return false;
		}

		/// <summary>
		/// 检查引号是否贴着普通字符。
		/// 规则：
		/// 1. 开始引号前面不能紧贴非空白字符；
		/// 2. 结束引号后面不能紧贴普通字符；
		/// 3. 结束引号后面如果是空白、行尾、注释则允许。
		/// </summary>
		private IEnumerable<ITagSpan<IErrorTag>> CreateInvalidInlineQuoteErrors(
			ITextSnapshot snapshot,
			ITextSnapshotLine line,
			string codeText)
		{
			bool inString = false;

			for (int i = 0; i < codeText.Length; i++)
			{
				if (codeText[i] != '"')
					continue;

				if (!inString)
				{
					// 当前是开始引号。
					// 开始引号必须位于参数起点，也就是：
					// 行首，或者前一个字符是空白。
					bool hasBadPreviousChar =
						i > 0 &&
						!char.IsWhiteSpace(codeText[i - 1]);

					if (hasBadPreviousChar)
					{
						yield return new TagSpan<IErrorTag>(
							new SnapshotSpan(snapshot, line.Start.Position + i, 1),
							new ErrorTag(PredefinedErrorTypeNames.SyntaxError,
							"引号前极度不建议紧贴普通字符，请用空格分隔，或把整个参数放进引号"));
					}

					inString = true;
				}
				else
				{
					// 当前是结束引号。
					// 结束引号后面允许：
					// 1. 行尾；
					// 2. 空白；
					// 3. 下一个 .keyword。
					bool hasBadNextChar =
						i + 1 < codeText.Length &&
						!char.IsWhiteSpace(codeText[i + 1]);

					if (hasBadNextChar)
					{
						if (codeText[i + 1] == '.')
							yield return new TagSpan<IErrorTag>(
								new SnapshotSpan(snapshot, line.Start.Position + i, 1),
								new ErrorTag(PredefinedErrorTypeNames.Warning,
								"引号后极度不建议紧贴下一个关键字，请用空格分隔"));
						else
							yield return new TagSpan<IErrorTag>(
								new SnapshotSpan(snapshot, line.Start.Position + i, 1),
								new ErrorTag(PredefinedErrorTypeNames.SyntaxError,
								"引号后极度不建议紧贴普通字符，请用空格分隔，或把整个参数放进引号"));
					}

					inString = false;
				}
			}
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

					// ------------------------------------------------------------
					// 注释至高优先级：
					// 只要本行出现 //，无论它是否在引号内部，
					// 从 // 开始到行尾都不参与任何报错判断。
					// 注意：这里只影响“报错判断”，不会修改原始文本。
					// SnapshotSpan 的位置仍然基于原始 lineText 的下标。
					// ------------------------------------------------------------
					int commentIndex = GetCommentStartIndex(lineText);
					string codeText = commentIndex >= 0
						? lineText.Substring(0, commentIndex)
						: lineText;

					// 空行、纯注释行不产生任何错误。
					if (string.IsNullOrWhiteSpace(codeText))
						continue;

					// effect 中出现行尾注释时给出警告，提示尽量避免这种写法。
					if (TryGetInlineCommentStart(lineText, out int inlineCommentIndex))
					{
						yield return new TagSpan<IErrorTag>(
							new SnapshotSpan(snapshot, line.Start.Position + inlineCommentIndex, 2),
							new ErrorTag(PredefinedErrorTypeNames.Warning, "请尽可能避免行内注释以防游戏识别错误"));
					}


					// ------------------------------------------------------------
					// 中文字符检查：
					// 只检查 // 前面的 codeText。
					// 因此注释里的中文允许存在，代码区的中文字符和中文标点全部报错。
					// ------------------------------------------------------------
					foreach (var chineseError in CreateChineseCharacterErrors(snapshot, line, codeText))
						yield return chineseError;

					// ------------------------------------------------------------
					// 半边引号检查：
					// 只检查 // 前面的 codeText，不跨行。
					// 如果 codeText 内双引号数量是奇数，说明这一行有未闭合引号。
					// ------------------------------------------------------------
					int quoteCount = codeText.Count(c => c == '"');
					if (quoteCount % 2 != 0)
					{
						int quoteIndex = codeText.LastIndexOf('"');

						yield return new TagSpan<IErrorTag>(
							new SnapshotSpan(
								snapshot,
								line.Start.Position + Math.Max(quoteIndex, 0),
								quoteIndex >= 0 ? 1 : Math.Max(1, codeText.Length)),
							new ErrorTag(
								PredefinedErrorTypeNames.SyntaxError,
								"单行内引号不成对"));
					}

					foreach (var quoteError in CreateInvalidInlineQuoteErrors(snapshot, line, codeText))
						yield return quoteError;

					int firstColonIndex = GetFirstLogicalColon(codeText);

					// 2. 结构校验
					if (firstColonIndex == -1)
                    {
                        // 如果本行没有冒号，也不是空行，那么它必须位于某个 effect: 块下方
                        if (!IsInsideEffectBlock(snapshot, i - 1))
                        {
                            yield return new TagSpan<IErrorTag>(
                                new SnapshotSpan(line.Start, codeText.Length),
                                new ErrorTag(PredefinedErrorTypeNames.SyntaxError, "此行不属于任何 'effect:'")
                            );
                        }
                        else
                        {
                            if (_keywordRegex.Match(codeText).Success)
                                yield return new TagSpan<IErrorTag>(
                                    new SnapshotSpan(line.Start, codeText.Length),
                                    new ErrorTag(PredefinedErrorTypeNames.Warning, "建议单条effect不在内部换行，如有需求请尽量用分行写法")
                                );
                            else
                                yield return new TagSpan<IErrorTag>(
                                    new SnapshotSpan(line.Start, codeText.Length),
                                    new ErrorTag(PredefinedErrorTypeNames.SyntaxError, "错误内容")
                                );
                        }
                    }
                    else
                    {
                        // 如果本行有冒号，它必须是 "effect:"
                        string header = codeText.Substring(0, firstColonIndex + 1).Trim();
                        if (!header.Equals("effect:", StringComparison.OrdinalIgnoreCase))
                        {
                            yield return new TagSpan<IErrorTag>(
                                new SnapshotSpan(line.Start, firstColonIndex + 1),
                                new ErrorTag(PredefinedErrorTypeNames.SyntaxError, $"无效的 Header '{header}'，Effect 文件需使用 'effect:'")
                            );
                        }
                    }

                    // 3. 关键字和参数校验
                    var stringMatches = _stringRegex.Matches(codeText).Cast<RegexMatch>().ToList();
                    var stringSpans = stringMatches.Select(m => new Span(m.Index, m.Length)).ToList();

					var seenDotKeywords = new Dictionary<string, RegexMatch>(StringComparer.Ordinal);

					foreach (RegexMatch match in _keywordRegex.Matches(codeText))
                    {
                        if (stringSpans.Any(s => s.Contains(match.Index))) continue;
                        if (match.Index > 0 && char.IsDigit(codeText[match.Index - 1])) continue;

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
								codeText,
								match.Index,
								match.Length,
								stringSpans,
								out int nameLength,
								out int nameValueStart,
								out int nameValueSpanLength,
								out bool isQuoted)) // 新增返回参数表示是否带引号
							{
								string nameValue = codeText.Substring(nameValueStart, nameValueSpanLength);

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

						if (keyword == ".heal" &&
							HasLaterKeyword(codeText, match.Index, ".healstress", stringSpans))
						{
							yield return new TagSpan<IErrorTag>(
								new SnapshotSpan(snapshot, line.Start + match.Index, match.Length),
								new ErrorTag(
									PredefinedErrorTypeNames.SyntaxError,
									".heal 写在 .healstress 前时不生效"));

							continue;
						}

						if (keyword == ".cure")
						{
							yield return new TagSpan<IErrorTag>(
								new SnapshotSpan(snapshot, line.Start + match.Index, match.Length),
								new ErrorTag(
									PredefinedErrorTypeNames.Suggestion,
									"由于.cure可能存在一些问题，建议换用.cure_bleed和.cure_poison"));
						}

						if (keyword == ".cure" &&
							HasLaterKeyword(codeText, match.Index, ".cure_disease", stringSpans))
						{
							yield return new TagSpan<IErrorTag>(
								new SnapshotSpan(snapshot, line.Start + match.Index, match.Length),
								new ErrorTag(
									PredefinedErrorTypeNames.SyntaxError,
									".cure 写在 .cure_disease 前时不生效，建议调整顺序或换用.cure_bleed和.cure_poison"));

							continue;
						}

						// ------------------------------------------------------------
						// dot 系列互斥检测：
						//
						// 这些关键字在同一个 effect 中不能同时出现任意两个：
						// .dotBleed / .dotPoison / .dotStress / .dotHpHeal / .dotShuffle
						//
						// 示例：
						// .dotBleed 1 .dotPoison 1
						//   -> 对 .dotPoison 报错
						// ------------------------------------------------------------
						if (DotKeywordsToCheck.Contains(keyword))
						{
							RegexMatch firstDifferentDotMatch = seenDotKeywords
								.Where(pair => !string.Equals(pair.Key, keyword, StringComparison.Ordinal))
								.Select(pair => pair.Value)
								.FirstOrDefault();

							if (firstDifferentDotMatch != null)
							{
								string firstDotKeyword = firstDifferentDotMatch.Value;

								yield return new TagSpan<IErrorTag>(
									new SnapshotSpan(snapshot, line.Start + match.Index, match.Length),
									new ErrorTag(
										PredefinedErrorTypeNames.SyntaxError,
										$"effect写法的流血、腐蚀、愈合、恐惧、延迟扰乱被写在同一行effect时，互相冲突，有且仅有一个效果能够生效，且结果与代码实际顺序无关。" +
										$"当effect写法的流血、腐蚀、愈合、恐惧、延迟扰乱被写在同一行effect并因此互相冲突时，最终生效者基于一个既定的优先级顺序，该优先级为：腐蚀 > 流血 > 恐惧 > 延迟扰乱 > 愈合。"));
							}

							// 只记录第一次出现的位置，后续重复出现同一关键字不覆盖。
							if (!seenDotKeywords.ContainsKey(keyword))
								seenDotKeywords.Add(keyword, match);
						}

						if (keyword == ".buff_ids" || keyword == ".set_monster_class_ids")
						{
							int pos = match.Index + match.Length;
							var args = new List<(int start, int length, string value)>();

							while (pos < codeText.Length)
							{
								// 跳过空白
								while (pos < codeText.Length && char.IsWhiteSpace(codeText[pos]))
									pos++;

								if (pos >= codeText.Length)
									break;

								// 遇到下一个 .keyword，说明当前关键字的参数结束
								if (IsKeywordStartAt(codeText, pos))
									break;

								int argStart = pos;
								int argLength;
								string argValue;

								if (codeText[pos] == '"')
								{
									int quoteStart = pos;
									int quoteEnd = codeText.IndexOf('"', quoteStart + 1);

									if (quoteEnd < 0)
										quoteEnd = codeText.Length;

									argStart = quoteStart + 1;
									argLength = Math.Max(0, quoteEnd - argStart);
									argValue = codeText.Substring(argStart, argLength);

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

									pos = quoteEnd < codeText.Length ? quoteEnd + 1 : codeText.Length;
								}
								else
								{
									int argEnd = pos;
									while (argEnd < codeText.Length && !char.IsWhiteSpace(codeText[argEnd]))
										argEnd++;

									argLength = argEnd - pos;
									argValue = codeText.Substring(pos, argLength);
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
										PredefinedErrorTypeNames.Suggestion,
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
								if (scanPos + 7 <= codeText.Length &&
	                                string.Compare(codeText, scanPos, "effect:", 0, 7, StringComparison.OrdinalIgnoreCase) == 0)
									break;

								// 检查 .target 出现
								if (scanPos + 7 <= codeText.Length && 
									string.Compare(codeText, scanPos, ".target", 0, 7, StringComparison.Ordinal) == 0)
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

						if (keyword == ".skill_instant")
						{
							var skillInstantParamMatch = _nextParamRegex.Match(codeText.Substring(match.Index + match.Length));
							if (skillInstantParamMatch.Success)
							{
								string skillInstantQuotedValue = skillInstantParamMatch.Groups[1].Value;
								string skillInstantPlainValue = skillInstantParamMatch.Groups[2].Value;
								bool isSkillInstantQuoted = skillInstantParamMatch.Groups[1].Success || skillInstantParamMatch.Value.Contains("\"\"");
								string skillInstantValue = isSkillInstantQuoted ? skillInstantQuotedValue : skillInstantPlainValue;

								// 当 .skill_instant 的参数为 true 时，向前向后扫描所属整个 effect 块，并忽略每行 // 后的注释内容。
								if (skillInstantValue == "true" && TryGetEffectBlockCodeRange(snapshot, i, out int blockStart, out int blockEnd, out string effectBlockCodeText))
								{
									bool hasTargetPerformerInSameEffect = false;

									foreach (RegexMatch sameEffectMatch in _keywordRegex.Matches(effectBlockCodeText))
									{
										if (string.Equals(sameEffectMatch.Value, ".target", StringComparison.Ordinal))
										{
											var targetParamMatch = _nextParamRegex.Match(effectBlockCodeText.Substring(sameEffectMatch.Index + sameEffectMatch.Length));
											if (targetParamMatch.Success)
											{
												string targetQuotedValue = targetParamMatch.Groups[1].Value;
												string targetPlainValue = targetParamMatch.Groups[2].Value;
												bool isTargetQuoted = targetParamMatch.Groups[1].Success || targetParamMatch.Value.Contains("\"\"");
												string targetValue = isTargetQuoted ? targetQuotedValue : targetPlainValue;

												if (targetValue == "performer")
												{
													hasTargetPerformerInSameEffect = true;
													break;
												}
											}
										}
									}

									if (!hasTargetPerformerInSameEffect)
									{
										string errorMsg = ".skill_instant要求目标必须是performer，否则在技能里会引起游戏崩溃或其他严重错误";
										var errorSpan = new SnapshotSpan(line.Snapshot, line.Start + match.Index, match.Length);
										yield return new TagSpan<IErrorTag>(errorSpan, new ErrorTag(PredefinedErrorTypeNames.SyntaxError, errorMsg));
									}
								}
							}
						}

						if (keyword == ".use_item_id")
						{
							// 当存在 .use_item_id 时，向前向后扫描所属整个 effect 块，并忽略每行 // 后的注释内容。
							bool hasUseItemTypeInSameEffect = false;

							if (TryGetEffectBlockCodeRange(snapshot, i, out int blockStart, out int blockEnd, out string effectBlockCodeText))
							{
								foreach (RegexMatch sameEffectMatch in _keywordRegex.Matches(effectBlockCodeText))
								{
									if (string.Equals(sameEffectMatch.Value, ".use_item_type", StringComparison.Ordinal))
									{
										hasUseItemTypeInSameEffect = true;
										break;
									}
								}
							}

							if (!hasUseItemTypeInSameEffect)
							{
								string errorMsg = ".use_item_id要求同一effect内必须同时存在.use_item_type";
								var errorSpan = new SnapshotSpan(line.Snapshot, line.Start + match.Index, match.Length);
								yield return new TagSpan<IErrorTag>(errorSpan, new ErrorTag(PredefinedErrorTypeNames.SyntaxError, errorMsg));
							}
						}
						if (keyword == ".daze")
						{
							yield return new TagSpan<IErrorTag>(
								new SnapshotSpan(snapshot, line.Start + match.Index, match.Length),
								new ErrorTag(
									PredefinedErrorTypeNames.Suggestion,
									$"不建议使用daze，若要使用，请保证理解其效果并避免崩溃与无效问题"));
						}

						if (keyword == ".guard" || keyword == ".clearguarded" || keyword == ".clearguarding")
						{
							var guardParamMatch = _nextParamRegex.Match(codeText.Substring(match.Index + match.Length));
							if (guardParamMatch.Success)
							{
								string guardQuotedValue = guardParamMatch.Groups[1].Value;
								string guardPlainValue = guardParamMatch.Groups[2].Value;
								bool isGuardQuoted = guardParamMatch.Groups[1].Success || guardParamMatch.Value.Contains("\"\"");
								string guardValue = isGuardQuoted ? guardQuotedValue : guardPlainValue;

								// 当守护相关关键字参数为 1 时，检查同一 effect 内的 .chance 是否小于 100%。
								if (guardValue == "1" && TryGetEffectBlockCodeRange(snapshot, i, out int blockStart, out int blockEnd, out string effectBlockCodeText))
								{
									bool hasChanceLowerThanHundredPercent = false;
									var chanceKeywordRegex = new Regex(@"\.chance\s+(?:""([^""]*)""|(\S+))", RegexOptions.Compiled);

									foreach (RegexMatch chanceMatch in chanceKeywordRegex.Matches(effectBlockCodeText))
									{
										string chanceValue;
										if (chanceMatch.Groups[1].Success)
										{
											chanceValue = chanceMatch.Groups[1].Value;
										}
										else
										{
											chanceValue = chanceMatch.Groups[2].Value;
										}
										string normalizedChanceValue = chanceValue.Trim();

										if (normalizedChanceValue.EndsWith("%", StringComparison.Ordinal))
										{
											string percentNumberText = normalizedChanceValue.Substring(0, normalizedChanceValue.Length - 1);
											if (double.TryParse(percentNumberText, out double percentValue) && percentValue < 100d)
											{
												hasChanceLowerThanHundredPercent = true;
												break;
											}
										}
										else if (double.TryParse(normalizedChanceValue, out double decimalChanceValue) && decimalChanceValue < 1d)
										{
											hasChanceLowerThanHundredPercent = true;
											break;
										}
									}

									if (hasChanceLowerThanHundredPercent)
									{
										string warningMsg = "守护和移除守护无法被.chance制约，若要实现概率性的相关效果，请自行寻找其他方案";
										var warningSpan = new SnapshotSpan(line.Snapshot, line.Start + match.Index, match.Length);
										yield return new TagSpan<IErrorTag>(warningSpan, new ErrorTag(PredefinedErrorTypeNames.Warning, warningMsg));
									}
								}
							}
						}
						if (keyword == ".affliction_blockable_chance")
						{
							yield return new TagSpan<IErrorTag>(
								new SnapshotSpan(snapshot, line.Start + match.Index, match.Length),
								new ErrorTag(
									PredefinedErrorTypeNames.Suggestion,
									$"不建议使用.affliction_blockable_chance，因为原版所有的折磨均未定义相关权重，此时使用该效果将会引发游戏崩溃"));
						}

						// 参数合法性检测
						if (DarkestEffectsData.KeywordToValuesMap.TryGetValue(keyword, out List<string> validValues))
                        {
							if (DarkestEffectsData.DoubleBoolKeywords.Contains(keyword))
							{
								// 双布尔关键字同时接受数字布尔与字符布尔，并允许字符布尔大小写变体。
								validValues = DarkestEffectsData.DoubleBoolValuesForError;
							}
							else if (DarkestEffectsData.KeywordToValuesMap[keyword] == DarkestEffectsData.StrBoolValues)
							{
								// 普通字符布尔关键字允许字符布尔大小写变体参与报错校验。
								validValues = DarkestEffectsData.StrBoolValuesForError;
							}
                            var remainingText = codeText.Substring(match.Index + match.Length);
                            var paramMatch = _nextParamRegex.Match(remainingText);
                            if (paramMatch.Success)
                            {
                                string valInQuote = paramMatch.Groups[1].Value;
                                string valPlain = paramMatch.Groups[2].Value;
                                bool isQuoted = paramMatch.Groups[1].Success || paramMatch.Value.Contains("\"\"");
                                string actualValue = isQuoted ? valInQuote : valPlain;

                                bool isParamValid = validValues.Contains(actualValue);
								bool isInvalidParamAllowed = false;

								// 计算参数值在原始行中的范围，供特殊警告精确标记到参数本身使用。
								int valOffset = isQuoted ? paramMatch.Value.IndexOf('"') : paramMatch.Value.IndexOf(actualValue);
								int valueStart = line.Start + match.Index + match.Length + paramMatch.Index + valOffset;
								int valueLen = isQuoted ? (paramMatch.Groups[1].Length + 2) : actualValue.Length;

								// 对于特殊参数的特判
								if (keyword == ".steal_buff_source_type")
								{
									if (actualValue == "bsrc_district")
									{
										int warningStart = line.Start + match.Index;
										int warningLen = match.Length;

										yield return new TagSpan<IErrorTag>(
											new SnapshotSpan(snapshot, warningStart, warningLen),
											new ErrorTag(PredefinedErrorTypeNames.Warning, "建筑源buff不可再生，请谨慎驱散")
										);
									}
									else if (actualValue == "bsrc_skill")
									{
										int warningStart = line.Start + match.Index;
										int warningLen = match.Length;

										yield return new TagSpan<IErrorTag>(
											new SnapshotSpan(snapshot, warningStart, warningLen),
											new ErrorTag(PredefinedErrorTypeNames.Warning, "谨慎驱散技能源，以防破坏他人机制")
										);
									}
									else if (!isParamValid)
									{
										isInvalidParamAllowed = true;

										int warningStart = line.Start + match.Index;
										int warningLen = match.Length;

										yield return new TagSpan<IErrorTag>(
											new SnapshotSpan(snapshot, warningStart, warningLen),
											new ErrorTag(PredefinedErrorTypeNames.Warning, "自定义Buff源将被视为combat_end源")
										);
									}
								}
								else if (keyword == ".steal_buff_stat_type")
								{
									bool isAllowedStealBuffStatType =
										actualValue == "hp_dot_bleed" ||
										actualValue == "hp_dot_poison" ||
										actualValue == "hp_dot_heal" ||
										actualValue == "stress_dot" ||
										actualValue == "shuffle_dot";

									if (!isAllowedStealBuffStatType)
									{
										int warningStart = line.Start + match.Index;
										int warningLen = match.Length;

										yield return new TagSpan<IErrorTag>(
											new SnapshotSpan(snapshot, warningStart, warningLen),
											new ErrorTag(PredefinedErrorTypeNames.Warning, "慎用超级真驱散，以免破坏他人机制")
										);
									}
								}
                                else if (keyword == ".buff_duration_type" && actualValue == "none")
								{
									yield return new TagSpan<IErrorTag>(
										new SnapshotSpan(snapshot, valueStart, valueLen),
										new ErrorTag(PredefinedErrorTypeNames.Warning, "在effect中，none这种持续类型将会被视为round")
									);
								}
								else if ((keyword == ".dotSource" || keyword == ".buff_source_type") && !isParamValid)
								{
									isInvalidParamAllowed = true;

									int warningStart = line.Start + match.Index;
									int warningLen = match.Length;

									yield return new TagSpan<IErrorTag>(
										new SnapshotSpan(snapshot, warningStart, warningLen),
										new ErrorTag(PredefinedErrorTypeNames.Warning, "自定义Buff源将被视为combat_end源")
									);
								}

								// 常规情况
								if (!isParamValid && !isInvalidParamAllowed)
                                {
                                    int errorStart = valueStart;
                                    int errorLen = valueLen;

                                    yield return new TagSpan<IErrorTag>(
                                        new SnapshotSpan(snapshot, errorStart, errorLen),
                                        new ErrorTag(PredefinedErrorTypeNames.SyntaxError, $"值 '{actualValue}' 对关键字 '{keyword}' 无效")
                                    );
                                }
                                else if (isQuoted && DarkestEffectsData.NumBoolValues.Contains(actualValue))
                                {
                                    int errorStart = valueStart;
                                    int errorLen = valueLen;

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
