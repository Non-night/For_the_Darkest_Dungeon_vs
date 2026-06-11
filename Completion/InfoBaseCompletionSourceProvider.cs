using For_the_Darkest_Dungeon.DefinitionDarkest;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace For_the_Darkest_Dungeon.Completion
{
	internal abstract class InfoBaseCompletionSourceProvider : ICompletionSourceProvider
	{
		/// <summary>
		/// 三个子类唯一需要提供的显示名称。
		///
		/// 例如：
		/// Info     -> DarkestInfo / Darkest Info Context
		/// Art      -> DarkestArt / Darkest Art Context
		/// Override -> DarkestOverride / Darkest Override Context
		/// </summary>
		protected abstract string KindName { get; }

		public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer)
		{
			return new InfoBaseCompletionSource(textBuffer, KindName);
		}

		private sealed class InfoBaseCompletionSource : ICompletionSource
		{
			private readonly ITextBuffer _buffer;
			private readonly string _kindName;
			private bool _isDisposed = false;

			// Header：允许行首存在空格或制表符。
			// 真正的 Header 名通过 header 捕获组取得，例如 "skill:"。
			private readonly Regex _headerRegex =
				new Regex(@"^[ \t]*(?<header>[a-zA-Z0-9_]+:)", RegexOptions.Compiled);

			public InfoBaseCompletionSource(ITextBuffer buffer, string kindName)
			{
				_buffer = buffer;
				_kindName = kindName;
			}

			public void AugmentCompletionSession(
				ICompletionSession session,
				IList<CompletionSet> completionSets)
			{
				if (_isDisposed)
					return;

				SnapshotPoint? triggerPoint = session.GetTriggerPoint(_buffer.CurrentSnapshot);
				if (!triggerPoint.HasValue)
					return;

				ITextSnapshot snapshot = _buffer.CurrentSnapshot;
				int curPos = triggerPoint.Value.Position;
				ITextSnapshotLine line = triggerPoint.Value.GetContainingLine();

				// 1. 获取光标前文本环境
				string lineTextUntilCaret = snapshot.GetText(line.Start, curPos - line.Start);
				string trimmedText = lineTextUntilCaret.TrimEnd();

				List<string> resultList = null;
				int startPos = curPos;

				// 2. 获取当前活动 Header
				string activeHeader = GetActiveHeader(snapshot, line.LineNumber);

				// 3-A. 参数值补全
				if (!string.IsNullOrEmpty(activeHeader) &&
					DarkestInfoData.InfoContextMap.TryGetValue(activeHeader, out List<string> keywords))
				{
					// 特判：连续参数补全，并排除已经出现过的参数。
					if (TryGetContinuousKeywordValueCompletion(
							lineTextUntilCaret,
							line.Start.Position,
							curPos,
							keywords,
							".disabled_popup_text_types",
							out resultList,
							out startPos)
						||
						TryGetContinuousKeywordValueCompletion(
							lineTextUntilCaret,
							line.Start.Position,
							curPos,
							keywords,
							".disabled_act_out_combat_start_turn_types",
							out resultList,
							out startPos))
					{
						// resultList 和 startPos 已经设置好
					}
					else
					{
						string foundKeyword = keywords.FirstOrDefault(kw => trimmedText.EndsWith(kw));
						if (foundKeyword != null)
						{
							resultList = DarkestInfoData.GetValuesForKeyword(activeHeader, foundKeyword);
							startPos = curPos;
						}
						else
						{
							string lastWord = trimmedText
								.Split(' ', '\t', '\n')
								.LastOrDefault();

							DarkestInfoData.KeywordValueMap.TryGetValue(
								".disabled_popup_text_types",
								out List<string> popupTextTypes);

							int disabledPopupStart = trimmedText.IndexOf(
								".disabled_popup_text_types",
								StringComparison.Ordinal);

							if (popupTextTypes != null &&
								popupTextTypes.Any(word => lastWord.EndsWith(word)) &&
								disabledPopupStart > -1)
							{
								resultList = DarkestInfoData.GetValuesForKeyword(
									activeHeader,
									".disabled_popup_text_types");

								startPos = curPos;
							}
						}
					}
				}

				// 3-B. 关键字或 Header 补全
				if (resultList == null || resultList.Count == 0)
				{
					int wordStart = curPos;

					while (wordStart > line.Start.Position)
					{
						char prevChar = snapshot[wordStart - 1];

						if (char.IsWhiteSpace(prevChar) || prevChar == ':')
							break;

						wordStart--;
					}

					string currentWord = snapshot.GetText(wordStart, curPos - wordStart);

					// 判断当前词前面是否只有空格 / 制表符。
					// 这样可以支持缩进后的 Header 补全。
					string textBeforeWord = snapshot.GetText(
						line.Start.Position,
						wordStart - line.Start.Position);

					bool beforeWordOnlyWhitespace =
						textBeforeWord.All(c => c == ' ' || c == '\t');

					bool currentLineHasColon =
						line.GetText().IndexOf(':') >= 0;

					// Header 补全
					if (!currentLineHasColon &&
						!currentWord.StartsWith(".") &&
						beforeWordOnlyWhitespace)
					{
						resultList = FuzzyCompletionCache.GetMatches(
							DarkestInfoData.AllHeaders,
							currentWord);

						startPos = wordStart;
					}
					// 关键字补全
					else if (!string.IsNullOrEmpty(activeHeader) &&
							 currentWord.StartsWith("."))
					{
						bool validDotStart = wordStart == line.Start.Position;

						if (!validDotStart && wordStart > line.Start.Position)
						{
							char prevChar = snapshot[wordStart - 1];
							validDotStart = char.IsWhiteSpace(prevChar) || prevChar == ':';
						}

						// 不允许 .hp. 这种 token 内第二个点继续触发关键字补全
						bool hasSecondDot = currentWord.IndexOf('.', 1) >= 0;

						if (validDotStart && !hasSecondDot)
						{
							if (DarkestInfoData.InfoContextMap.TryGetValue(
								activeHeader,
								out List<string> kws))
							{
								resultList = FuzzyCompletionCache.GetMatches(
									kws,
									currentWord);

								startPos = wordStart;
							}
						}
					}
				}

				// 4. 生成补全集合
				if (resultList != null && resultList.Count > 0)
				{
					string completionSetName = "Darkest" + _kindName;
					string contextText = "Darkest " + _kindName + " Context: " + activeHeader;

					List<Microsoft.VisualStudio.Language.Intellisense.Completion> completions =
						resultList
							.Select(k => new Microsoft.VisualStudio.Language.Intellisense.Completion(
								k,
								k,
								contextText,
								null,
								null))
							.ToList();

					ITrackingSpan applicableTo = snapshot.CreateTrackingSpan(
						new Span(startPos, Math.Max(0, curPos - startPos)),
						SpanTrackingMode.EdgeInclusive);

					completionSets.Add(new FuzzyCompletionSet(
						completionSetName,
						completionSetName,
						applicableTo,
						completions,
						null));
				}
			}

			/// <summary>
			/// 获取当前活动 Header。
			/// 优先当前行，其次向上追溯。
			/// </summary>
			private string GetActiveHeader(ITextSnapshot snapshot, int currentLineNumber)
			{
				for (int i = currentLineNumber; i >= 0; i--)
				{
					ITextSnapshotLine line = snapshot.GetLineFromLineNumber(i);
					string text = line.GetText();

					// 跳过注释和空行
					if (string.IsNullOrWhiteSpace(text) || text.TrimStart().StartsWith("//"))
						continue;

					Match match = _headerRegex.Match(text);
					if (match.Success)
					{
						return match.Groups["header"].Value;
					}
				}

				return null;
			}

			/// <summary>
			/// 连续参数补全特判。
			///
			/// 用于这类关键字：
			/// .xxx value1 value2 value3 ...
			///
			/// 功能：
			/// 1. 支持连续参数补全；
			/// 2. 支持当前正在输入的参数过滤；
			/// 3. 排除前文已经出现过的参数；
			/// 4. 只在当前 Header 允许该关键字时生效。
			///
			/// 当前用于：
			/// - .disabled_popup_text_types
			/// - .disabled_act_out_combat_start_turn_types
			/// </summary>
			private bool TryGetContinuousKeywordValueCompletion(
				string lineTextUntilCaret,
				int lineStartPosition,
				int curPos,
				List<string> keywords,
				string keyword,
				out List<string> resultList,
				out int startPos)
			{
				resultList = null;
				startPos = curPos;

				if (string.IsNullOrEmpty(keyword))
					return false;

				if (keywords == null || !keywords.Contains(keyword))
					return false;

				if (!DarkestInfoData.KeywordValueMap.TryGetValue(keyword, out List<string> allValues))
					return false;

				if (allValues == null || allValues.Count == 0)
					return false;

				int keywordIndex = lineTextUntilCaret.LastIndexOf(keyword, StringComparison.Ordinal);
				if (keywordIndex < 0)
					return false;

				int afterKeywordIndex = keywordIndex + keyword.Length;

				// 必须已经进入参数区域。
				// 也就是说，关键字后面至少要有空白。
				// 这样不会干扰正在输入这个关键字本身的补全。
				if (afterKeywordIndex >= lineTextUntilCaret.Length)
					return false;

				if (!char.IsWhiteSpace(lineTextUntilCaret[afterKeywordIndex]))
					return false;

				// 当前正在输入的参数 token 起点。
				// 例如：
				// ".disabled_act_out_combat_start_turn_types affliction virt"
				// 当前 token 是 "virt"，tokenStartIndex 指向 v。
				int tokenStartIndex = lineTextUntilCaret.Length;

				while (tokenStartIndex > afterKeywordIndex)
				{
					char prevChar = lineTextUntilCaret[tokenStartIndex - 1];

					if (char.IsWhiteSpace(prevChar))
						break;

					tokenStartIndex--;
				}

				string currentInput = lineTextUntilCaret.Substring(
					tokenStartIndex,
					lineTextUntilCaret.Length - tokenStartIndex);

				// 已经完成输入的参数区域，不包含当前正在输入的 token。
				string completedArgumentText = lineTextUntilCaret.Substring(
					afterKeywordIndex,
					tokenStartIndex - afterKeywordIndex);

				HashSet<string> usedValues = new HashSet<string>(
					completedArgumentText
						.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries),
					StringComparer.OrdinalIgnoreCase);

				// 排除已经出现过的参数。
				List<string> availableValues = allValues
					.Where(v => !usedValues.Contains(v))
					.ToList();

				if (availableValues.Count == 0)
					return false;

				resultList = FuzzyCompletionCache.GetMatches(
					availableValues,
					currentInput);

				startPos = lineStartPosition + tokenStartIndex;

				return resultList != null && resultList.Count > 0;
			}

			public void Dispose()
			{
				_isDisposed = true;
			}
		}
	}
}