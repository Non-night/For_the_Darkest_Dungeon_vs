using For_the_Darkest_Dungeon.DefinitionDarkest;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace For_the_Darkest_Dungeon.Completion
{
	/// <summary>
	/// Info / Art / Override 三类文件共用的补全源提供器基类。
	///
	/// 这三类文件在补全规则上必须保持完全一致，
	/// 因此所有关键字补全、Header 补全、静态值补全、连续参数补全都统一集中在这里。
	/// </summary>
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
			/// <summary>
			/// 所有需要支持连续参数补全的关键字。
			/// 按你的要求，Info / Art / Override 三者完全统一，并以 Info 为基准。
			/// </summary>
			private static readonly string[] ContinuousValueKeywords =
			{
				".disabled_popup_text_types",
				".disabled_act_out_combat_start_turn_types"
			};

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

				// 1. 获取光标前文本环境。
				string lineTextUntilCaret = snapshot.GetText(line.Start, curPos - line.Start);
				string trimmedText = lineTextUntilCaret.TrimEnd();

				List<string> resultList = null;
				int startPos = curPos;

				// 2. 获取当前活动 Header。
				string activeHeader = GetActiveHeader(snapshot, line.LineNumber);

				// 3-A. 参数值补全。
				// 这里先判断“当前 token 是否明显是关键字输入”。
				// 如果当前 token 以 . 开头，则优先走关键字补全，
				// 不允许连续参数补全把 . 开头的输入抢走。
				bool currentTokenStartsWithDot = TryGetCurrentToken(lineTextUntilCaret, out string currentToken)
					&& currentToken.StartsWith(".", StringComparison.Ordinal);

				if (!currentTokenStartsWithDot &&
					!string.IsNullOrEmpty(activeHeader) &&
					DarkestInfoData.InfoContextMap.TryGetValue(activeHeader, out List<string> keywords))
				{
					// 特判：连续参数补全，并排除已经出现过的参数。
					if (TryGetContinuousKeywordValueCompletion(
							lineTextUntilCaret,
							line.Start.Position,
							curPos,
							keywords,
							out resultList,
							out startPos))
					{
						// resultList 和 startPos 已经设置好。
					}
					else
					{
						string foundKeyword = keywords.FirstOrDefault(keyword => trimmedText.EndsWith(keyword));
						if (foundKeyword != null)
						{
							resultList = DarkestInfoData.GetValuesForKeyword(activeHeader, foundKeyword);
							startPos = curPos;
						}
						else
						{
							// 如果当前已经处在连续参数序列中，而且最后一个词是前面刚输入的参数值，
							// 仍然继续给出该连续关键字的候选值列表。
							foreach (string continuousKeyword in ContinuousValueKeywords)
							{
								if (TryGetFollowupContinuousValueCompletion(
										trimmedText,
										activeHeader,
										continuousKeyword,
										out resultList,
										out startPos,
										curPos))
								{
									break;
								}
							}
						}
					}
				}

				// 3-B. 关键字或 Header 补全。
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

					// Header 补全。
					if (!currentLineHasColon &&
						!currentWord.StartsWith(".") &&
						beforeWordOnlyWhitespace)
					{
						resultList = FuzzyCompletionCache.GetMatches(
							DarkestInfoData.AllHeaders,
							currentWord);

						startPos = wordStart;
					}
					// 关键字补全。
					else if (currentWord.StartsWith("."))
					{
						if (!string.IsNullOrEmpty(activeHeader) &&
							DarkestInfoData.InfoContextMap.TryGetValue(
								activeHeader,
								out List<string> kws))
						{
							// 关键字补全需要按整个同类 Header 块去重，已经在同一块内出现过的关键字不再重复给出。
							resultList = FuzzyCompletionCache.GetMatches(
								GetAvailableHeaderKeywords(snapshot, line.LineNumber, activeHeader, kws, currentWord),
								currentWord);

							startPos = wordStart;
						}
					}
				}

				// 4. 生成补全集合。
				if (resultList != null && resultList.Count > 0)
				{
					string completionSetName = "Darkest" + _kindName;
					string contextText = "Darkest " + _kindName + " Context: " + activeHeader;

					List<Microsoft.VisualStudio.Language.Intellisense.Completion> completions =
						resultList
							.Select(item => new Microsoft.VisualStudio.Language.Intellisense.Completion(
								item,
								item,
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
			/// 获取当前 Header 整块中仍可用于关键字补全的候选列表。
			/// 已经在同一 Header 块内出现过的关键字不会重复出现在补全中。
			/// </summary>
			private List<string> GetAvailableHeaderKeywords(
				ITextSnapshot snapshot,
				int currentLineNumber,
				string activeHeader,
				List<string> allKeywords,
				string currentInput)
			{
				HashSet<string> usedKeywords = GetUsedHeaderKeywords(snapshot, currentLineNumber, activeHeader);
				usedKeywords.Remove(currentInput);

				return allKeywords
					.Where(keyword => !usedKeywords.Contains(keyword))
					.ToList();
			}

			/// <summary>
			/// 扫描当前所在的整个 Header 块，提取其中已经出现过的所有关键字。
			/// </summary>
			private HashSet<string> GetUsedHeaderKeywords(
				ITextSnapshot snapshot,
				int currentLineNumber,
				string activeHeader)
			{
				HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				Regex keywordRegex = new Regex(@"\.[a-zA-Z_][a-zA-Z0-9_]*", RegexOptions.Compiled);

				if (!TryGetHeaderBlockRange(snapshot, currentLineNumber, activeHeader, out int startLine, out int endLine))
				{
					return result;
				}

				for (int lineNumber = startLine; lineNumber <= endLine; lineNumber++)
				{
					ITextSnapshotLine blockLine = snapshot.GetLineFromLineNumber(lineNumber);
					string lineText = blockLine.GetText();
					int commentIndex = lineText.IndexOf("//", StringComparison.Ordinal);
					string codeText = commentIndex >= 0 ? lineText.Substring(0, commentIndex) : lineText;

					foreach (Match keywordMatch in keywordRegex.Matches(codeText))
					{
						result.Add(keywordMatch.Value);
					}
				}

				return result;
			}

			/// <summary>
			/// 获取当前行所在的整个同类 Header 块范围。
			/// </summary>
			private bool TryGetHeaderBlockRange(
				ITextSnapshot snapshot,
				int currentLineNumber,
				string activeHeader,
				out int startLine,
				out int endLine)
			{
				startLine = -1;
				endLine = -1;

				if (string.IsNullOrEmpty(activeHeader))
				{
					return false;
				}

				for (int i = currentLineNumber; i >= 0; i--)
				{
					ITextSnapshotLine blockLine = snapshot.GetLineFromLineNumber(i);
					Match match = _headerRegex.Match(blockLine.GetText());
					if (match.Success && string.Equals(match.Groups["header"].Value, activeHeader, StringComparison.OrdinalIgnoreCase))
					{
						startLine = i;
						break;
					}
				}

				if (startLine < 0)
				{
					return false;
				}

				endLine = snapshot.LineCount - 1;
				for (int i = startLine + 1; i < snapshot.LineCount; i++)
				{
					ITextSnapshotLine blockLine = snapshot.GetLineFromLineNumber(i);
					Match match = _headerRegex.Match(blockLine.GetText());
					if (match.Success)
					{
						endLine = i - 1;
						break;
					}
				}

				return true;
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

					// 跳过注释和空行。
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
			/// 尝试获取当前 token。
			/// token 以空白或冒号为边界。
			/// </summary>
			private bool TryGetCurrentToken(string lineTextUntilCaret, out string currentToken)
			{
				currentToken = string.Empty;

				if (string.IsNullOrEmpty(lineTextUntilCaret))
					return false;

				int tokenStart = lineTextUntilCaret.Length;
				while (tokenStart > 0)
				{
					char prevChar = lineTextUntilCaret[tokenStart - 1];
					if (char.IsWhiteSpace(prevChar) || prevChar == ':')
						break;

					tokenStart--;
				}

				currentToken = lineTextUntilCaret.Substring(tokenStart);
				return !string.IsNullOrEmpty(currentToken);
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
			/// 4. 只在当前 Header 允许该关键字时生效；
			/// 5. 若当前 token 以 . 开头，则不抢关键字补全。
			/// </summary>
			private bool TryGetContinuousKeywordValueCompletion(
				string lineTextUntilCaret,
				int lineStartPosition,
				int curPos,
				List<string> keywords,
				out List<string> resultList,
				out int startPos)
			{
				resultList = null;
				startPos = curPos;

				foreach (string keyword in ContinuousValueKeywords)
				{
					if (TryGetContinuousValueCompletionForOneKeyword(
							lineTextUntilCaret,
							lineStartPosition,
							curPos,
							keywords,
							keyword,
							out resultList,
							out startPos))
					{
						return true;
					}
				}

				return false;
			}

			private bool TryGetContinuousValueCompletionForOneKeyword(
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

				// 若当前输入已经以 . 开头，则说明用户正在开始下一个关键字输入。
				// 按你的要求，此时必须优先触发关键字补全，而不是参数补全。
				if (currentInput.StartsWith(".", StringComparison.Ordinal))
					return false;

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
					.Where(value => !usedValues.Contains(value))
					.ToList();

				if (availableValues.Count == 0)
					return false;

				resultList = FuzzyCompletionCache.GetMatches(
					availableValues,
					currentInput);

				startPos = lineStartPosition + tokenStartIndex;

				return resultList != null && resultList.Count > 0;
			}

			/// <summary>
			/// 当用户已经输入了一个连续参数值，并准备继续输入下一个值时，
			/// 仍然继续给出该连续关键字允许的候选值。
			/// </summary>
			private bool TryGetFollowupContinuousValueCompletion(
				string trimmedText,
				string activeHeader,
				string keyword,
				out List<string> resultList,
				out int startPos,
				int curPos)
			{
				resultList = null;
				startPos = curPos;

				if (string.IsNullOrEmpty(activeHeader))
					return false;

				string lastWord = trimmedText
					.Split(' ', '\t', '\n')
					.LastOrDefault();

				if (string.IsNullOrEmpty(lastWord) || lastWord.StartsWith(".", StringComparison.Ordinal))
					return false;

				if (!DarkestInfoData.KeywordValueMap.TryGetValue(keyword, out List<string> values))
					return false;

				int keywordStart = trimmedText.IndexOf(keyword, StringComparison.Ordinal);
				if (keywordStart < 0)
					return false;

				if (!values.Any(value => lastWord.EndsWith(value)))
					return false;

				resultList = DarkestInfoData.GetValuesForKeyword(activeHeader, keyword);
				startPos = curPos;
				return resultList != null && resultList.Count > 0;
			}

			public void Dispose()
			{
				_isDisposed = true;
			}
		}
	}
}