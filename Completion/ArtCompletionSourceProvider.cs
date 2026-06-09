using For_the_Darkest_Dungeon.DefinitionDarkest;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text.RegularExpressions;

namespace For_the_Darkest_Dungeon.Completion
{
	[Export(typeof(ICompletionSourceProvider))]
	[ContentType("darkest-art")]
	[Name("Darkest Art Completion Provider")]
	internal class ArtCompletionSourceProvider : ICompletionSourceProvider
	{
		public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer)
		{
			return new ArtCompletionSource(textBuffer);
		}
	}

	internal class ArtCompletionSource : ICompletionSource
	{
		private readonly ITextBuffer _buffer;
		private bool _isDisposed = false;
		private readonly Regex _headerRegex = new Regex(@"^[a-zA-Z0-9_]+:", RegexOptions.Compiled);

		public ArtCompletionSource(ITextBuffer buffer)
		{
			_buffer = buffer;
		}

		public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets)
		{
			if (_isDisposed) return;

			SnapshotPoint? triggerPoint = session.GetTriggerPoint(_buffer.CurrentSnapshot);
			if (!triggerPoint.HasValue) return;

			ITextSnapshot snapshot = _buffer.CurrentSnapshot;
			int curPos = triggerPoint.Value.Position;
			var line = triggerPoint.Value.GetContainingLine();

			// 1. 获取光标前后的文本环境
			string lineTextUntilCaret = snapshot.GetText(line.Start, curPos - line.Start);
			string trimmedText = lineTextUntilCaret.TrimEnd();

			List<string> resultList = null;
			int startPos = curPos;

			// --- 2. 确定上下文 Header ---
			// 逻辑：如果当前行有冒号，使用当前行的；否则向上追溯
			string activeHeader = GetActiveHeader(snapshot, line.LineNumber);

			// --- 3. 补全决策引擎 ---

			// A. 参数值补全 (如 .hp 100 这里的 100)
			if (!string.IsNullOrEmpty(activeHeader) && DarkestInfoData.InfoContextMap.TryGetValue(activeHeader, out var keywords))
			{
				// 特判：.disabled_popup_text_types 支持连续参数补全，并排除已经出现过的参数。
				// 注意：这个判断只处理 .disabled_popup_text_types，不影响其他关键字。
				if (TryGetDisabledPopupTextTypesCompletion(
					lineTextUntilCaret,
					line.Start.Position,
					curPos,
					keywords,
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
						startPos = curPos; // 参数通常直接追加，不覆盖前文
					}
					else
					{
						string lastWord = trimmedText.Split(' ', '\t', '\n').LastOrDefault();
						DarkestInfoData.KeywordValueMap.TryGetValue(".disabled_popup_text_types", out List<string> l);
						int dispopupStart = trimmedText.IndexOf(".disabled_popup_text_types");
						if (l != null && l.Any(word => lastWord.EndsWith(word)) && dispopupStart > -1)
						{
							resultList = DarkestInfoData.GetValuesForKeyword(activeHeader, ".disabled_popup_text_types");
							startPos = curPos;
						}
					}
				}
			}

			// B. 关键字或 Header 补全
			if (resultList == null || resultList.Count == 0)
			{
				// 计算当前正在打的词的起始位置
				int wordStart = curPos;
				while (wordStart > line.Start.Position)
				{
					char prevChar = snapshot[wordStart - 1];
					// 停止符：空格、冒号、或者如果当前已经在单词内遇到点号
					if (char.IsWhiteSpace(prevChar) || prevChar == ':') break;
					wordStart--;
				}

				string currentWord = snapshot.GetText(wordStart, curPos - wordStart);

				// 判断：是在写行首 Header，还是在写关键字
				// 如果当前行还没冒号，且当前词不以 "." 开头，且处于行首附近 -> 提示 Header
				if (string.IsNullOrEmpty(line.GetText().Split(':').FirstOrDefault(s => s.Contains(":")))
					&& !currentWord.StartsWith(".")
					&& wordStart == line.Start.Position)
				{
					resultList = FuzzyCompletionCache.GetMatches(
						DarkestInfoData.AllHeaders,
						currentWord);

					startPos = wordStart;
				}
				else if (!string.IsNullOrEmpty(activeHeader) && currentWord.StartsWith("."))
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
						if (DarkestInfoData.InfoContextMap.TryGetValue(activeHeader, out var kws))
						{
							resultList = FuzzyCompletionCache.GetMatches(
								kws,
								currentWord);

							startPos = wordStart;
						}
					}
				}
			}

			// --- 4. 生成补全集合 ---
			if (resultList != null && resultList.Count > 0)
			{
				var completions = resultList.Select(k =>
					new Microsoft.VisualStudio.Language.Intellisense.Completion(k, k, "Darkest Art Context: " + activeHeader, null, null)).ToList();

				var applicableTo = snapshot.CreateTrackingSpan(
					new Span(startPos, Math.Max(0, curPos - startPos)),
					SpanTrackingMode.EdgeInclusive);

				completionSets.Add(new FuzzyCompletionSet(
					"DarkestArt",
					"DarkestArt",
					applicableTo,
					completions,
					null));
			}
		}

		/// <summary>
		/// 核心逻辑：获取当前活动的 Header。优先当前行，其次向上追溯。
		/// </summary>
		private string GetActiveHeader(ITextSnapshot snapshot, int currentLineNumber)
		{
			for (int i = currentLineNumber; i >= 0; i--)
			{
				var line = snapshot.GetLineFromLineNumber(i);
				string text = line.GetText();

				// 跳过注释和空行
				if (string.IsNullOrWhiteSpace(text) || text.TrimStart().StartsWith("//"))
					continue;

				var match = _headerRegex.Match(text);
				if (match.Success)
				{
					return match.Value;
				}
			}
			return null;
		}

		/// <summary>
		/// 特判 .disabled_popup_text_types：
		/// 1. 支持连续参数补全：.disabled_popup_text_types bleed poison ...
		/// 2. 支持当前正在输入的参数过滤：.disabled_popup_text_types bl
		/// 3. 排除前文已经出现过的参数。
		/// 4. 只在当前 header 允许 .disabled_popup_text_types 时生效，不干扰其他关键字。
		/// </summary>
		private bool TryGetDisabledPopupTextTypesCompletion(
			string lineTextUntilCaret,
			int lineStartPosition,
			int curPos,
			List<string> keywords,
			out List<string> resultList,
			out int startPos)
		{
			resultList = null;
			startPos = curPos;

			const string keyword = ".disabled_popup_text_types";

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
			// 也就是说，".disabled_popup_text_types" 后面至少要有空白。
			// 这样不会干扰正在输入这个关键字本身的补全。
			if (afterKeywordIndex >= lineTextUntilCaret.Length)
				return false;

			if (!char.IsWhiteSpace(lineTextUntilCaret[afterKeywordIndex]))
				return false;

			// 当前正在输入的参数 token 起点。
			// 例如：
			// ".disabled_popup_text_types bleed po"
			// 当前 token 是 "po"，tokenStartIndex 指向 p。
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

			// 继续使用你现有的缓存模糊匹配。
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