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
	// 这个 Export 标签至关重要，它告诉 VS 这是一个补全提供者
	[Export(typeof(ICompletionSourceProvider))]
	[ContentType("darkest-effect")] // 必须和你在 Definition 里定义的名字一模一样
	[Name("Darkest Effect Completion Provider")]
	internal class EffectCompletionSourceProvider : ICompletionSourceProvider
	{
		// VS 会调用这个方法来获取补全源
		public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer)
		{
			// 为每一个打开的文本缓冲区创建一个新的 Source 实例
			return new EffectCompletionSource(textBuffer);
		}
	}

	internal class EffectCompletionSource : ICompletionSource
	{
		private static readonly HashSet<string> DotEffectKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			// 这 5 个才是需要互斥处理的合法 dot 类关键字，不包含 .dotSource。
			".dotBleed",
			".dotPoison",
			".dotStress",
			".dotHpHeal",
			".dotShuffle"
		};

		private readonly ITextBuffer _buffer;
		private bool _isDisposed = false;

		public EffectCompletionSource(ITextBuffer buffer)
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

			// 获取行内光标前的所有文本
			string lineTextUntilCaret = snapshot.GetText(line.Start, curPos - line.Start);
			string trimmedText = lineTextUntilCaret.TrimEnd();

			List<string> sourceList;
			int start = curPos;

			// 上下文逻辑切换
			// 上下文逻辑切换
			// 先尝试参数补全：支持 ".target " 后直接弹出，也支持 ".target e" 继续过滤
			if (TryGetEffectParameterCompletion(
					snapshot,
					line.Start.Position,
					curPos,
					out sourceList,
					out start))
			{
				// sourceList 和 start 已经在 TryGetEffectParameterCompletion 里设置好
			}
			else
			{
				// 关键字补全：必须从 . 开始
				// 从光标往前回溯，直到遇到 .、空白字符或行首
				bool foundDot = false;

				while (start > line.Start.Position)
				{
					char prevChar = snapshot[start - 1];

					if (prevChar == '.')
					{
						start--; // 包含点号，使 Span 覆盖从 . 到光标的位置
						foundDot = true;
						break;
					}

					if (char.IsWhiteSpace(prevChar))
					{
						break;
					}

					start--;
				}

				// 如果从光标处开始一直回溯到空格/行首都没有出现 .，则不该引发补全
				if (!foundDot)
					return;

				// 只允许 . 位于行首、空白后、或冒号后。
				// 防止 abc.hp 或 .hp. 这种非法位置也生成关键字补全。
				if (start > line.Start.Position)
				{
					char prevChar = snapshot[start - 1];
					if (!char.IsWhiteSpace(prevChar) && prevChar != ':')
						return;
				}

				string currentInput = snapshot.GetText(start, curPos - start);

				// 不允许 token 内出现第二个点，例如 .hp.
				if (currentInput.IndexOf('.', 1) >= 0)
					return;

				// 使用模糊匹配生成候选列表。
				// 关键字补全需要按整个 effect 块去重，已经在同一块内出现过的关键字不再重复给出。
				sourceList = FuzzyCompletionCache.GetMatches(
					GetAvailableEffectKeywords(snapshot, line.LineNumber, currentInput),
					currentInput);
			}

			if (sourceList == null || sourceList.Count == 0) return;

			// 创建补全集
			var completions = sourceList.Select(k =>
				new Microsoft.VisualStudio.Language.Intellisense.Completion(k, k, "Darkest Dungeon", null, null)).ToList();

			// 确保 Span 合法
			var applicableTo = snapshot.CreateTrackingSpan(
				new Span(start, Math.Max(0, curPos - start)),
				SpanTrackingMode.EdgeInclusive);

			completionSets.Add(new FuzzyCompletionSet(
				"DarkestEffects",
				"DarkestEffects",
				applicableTo,
				completions,
				null));
		}

		/// <summary>
		/// 获取当前 effect 整块中仍可用于关键字补全的候选列表。
		/// 已经在同一 effect 块内出现过的关键字不会重复出现在补全中。
		/// </summary>
		private List<string> GetAvailableEffectKeywords(
			ITextSnapshot snapshot,
			int currentLineNumber,
			string currentInput)
		{
			HashSet<string> usedKeywords = GetUsedEffectKeywords(snapshot, currentLineNumber, currentInput);

			IEnumerable<string> availableKeywords = DarkestEffectsData.AllKeywords
				.Where(keyword => !usedKeywords.Contains(keyword));

			// 同一条 effect 内只要已经出现过任意一个合法 dot 类关键字，
			// 后续补全中就不再显示任何 dot 类关键字。
			if (HasAnyUsedDotEffectKeyword(usedKeywords))
			{
				availableKeywords = availableKeywords.Where(keyword => !DotEffectKeywords.Contains(keyword));
			}

			return availableKeywords.ToList();
		}

		/// <summary>
		/// 判断当前 effect 块中是否已经出现过任意一个合法 dot 类关键字。
		/// </summary>
		private bool HasAnyUsedDotEffectKeyword(HashSet<string> usedKeywords)
		{
			return usedKeywords.Any(keyword => DotEffectKeywords.Contains(keyword));
		}

		/// <summary>
		/// 扫描当前所在的整个 effect 块，提取其中已经出现过的所有关键字。
		/// </summary>
		private HashSet<string> GetUsedEffectKeywords(
			ITextSnapshot snapshot,
			int currentLineNumber,
			string currentInput)
		{
			HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			Regex keywordRegex = new Regex(@"\.[a-zA-Z_][a-zA-Z0-9_]*", RegexOptions.Compiled);

			if (!TryGetEffectBlockRange(snapshot, currentLineNumber, out int startLine, out int endLine))
			{
				return result;
			}

			for (int lineNumber = startLine; lineNumber <= endLine; lineNumber++)
			{
				ITextSnapshotLine blockLine = snapshot.GetLineFromLineNumber(lineNumber);
				string codeText = TrimCommentFromLine(blockLine.GetText());

				// 当前输入行末尾正在输入的未完成关键字不能算作“已使用”，
				// 否则会把模糊匹配本应给出的候选项提前过滤掉。
				if (lineNumber == currentLineNumber &&
					!string.IsNullOrEmpty(currentInput) &&
					codeText.EndsWith(currentInput, StringComparison.Ordinal))
				{
					codeText = codeText.Substring(0, codeText.Length - currentInput.Length);
				}

				foreach (Match keywordMatch in keywordRegex.Matches(codeText))
				{
					result.Add(keywordMatch.Value);
				}
			}

			return result;
		}

		/// <summary>
		/// 获取当前行所在的整个 effect 块范围。
		/// 这里要与 Effect 的现有语法规则保持一致：
		/// 先移除 // 注释，再按逻辑冒号判断 header，只有 header 为 effect: 时才视为块边界。
		/// </summary>
		private bool TryGetEffectBlockRange(
			ITextSnapshot snapshot,
			int currentLineNumber,
			out int startLine,
			out int endLine)
		{
			startLine = -1;
			endLine = -1;

			for (int i = currentLineNumber; i >= 0; i--)
			{
				string lineText = snapshot.GetLineFromLineNumber(i).GetText();
				string codeText = TrimCommentFromLine(lineText);
				int colonIndex = GetFirstLogicalColon(codeText);

				if (colonIndex < 0)
					continue;

				string header = codeText.Substring(0, colonIndex + 1).Trim();
				if (string.Equals(header, "effect:", StringComparison.OrdinalIgnoreCase))
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
				string lineText = snapshot.GetLineFromLineNumber(i).GetText();
				string codeText = TrimCommentFromLine(lineText);
				int colonIndex = GetFirstLogicalColon(codeText);

				if (colonIndex < 0)
					continue;

				string header = codeText.Substring(0, colonIndex + 1).Trim();
				if (string.Equals(header, "effect:", StringComparison.OrdinalIgnoreCase))
				{
					endLine = i - 1;
					break;
				}
			}

			return true;
		}

		/// <summary>
		/// 移除一行中 // 之后的注释内容。
		/// </summary>
		private string TrimCommentFromLine(string lineText)
		{
			int commentIndex = lineText.IndexOf("//", StringComparison.Ordinal);
			return commentIndex >= 0 ? lineText.Substring(0, commentIndex) : lineText;
		}

		/// <summary>
		/// 获取一行中第一个逻辑冒号的位置。
		/// 当前补全逻辑只需要与现有效果块边界规则保持一致，因此这里不额外处理字符串字面量。
		/// </summary>
		private int GetFirstLogicalColon(string lineText)
		{
			return lineText.IndexOf(':');
		}

		/// <summary>
		/// 从当前行已输入的代码中提取指定 effect 关键字的第一个参数值。
		/// 这里会忽略当前行 // 之后的内容，以与 effect 语法解析规则保持一致。
		/// </summary>
		private bool TryGetEffectKeywordValueFromCurrentLine(
			string lineText,
			string keyword,
			out string actualValue)
		{
			actualValue = null;

			int commentIndex = lineText.IndexOf("//", StringComparison.Ordinal);
			string codeText = commentIndex >= 0 ? lineText.Substring(0, commentIndex) : lineText;

			var keywordRegex = new Regex(@"\.[a-zA-Z_][a-zA-Z0-9_]*", RegexOptions.Compiled);
			var nextParamRegex = new Regex(@"^\s+(?:""([^""]*)""|([^\s]+))", RegexOptions.Compiled);

			foreach (Match keywordMatch in keywordRegex.Matches(codeText))
			{
				if (!string.Equals(keywordMatch.Value, keyword, StringComparison.Ordinal))
					continue;

				Match paramMatch = nextParamRegex.Match(codeText.Substring(keywordMatch.Index + keywordMatch.Length));
				if (!paramMatch.Success)
					return false;

				actualValue = paramMatch.Groups[1].Success ? paramMatch.Groups[1].Value : paramMatch.Groups[2].Value;
				return true;
			}

			return false;
		}

		private bool TryGetEffectParameterCompletion(
			ITextSnapshot snapshot,
			int lineStart,
			int curPos,
			out List<string> sourceList,
			out int argumentStart)
		{
			sourceList = null;
			argumentStart = curPos;

			// 找当前参数 token 的起点。
			int tokenStart = curPos;
			while (tokenStart > lineStart)
			{
				char prev = snapshot[tokenStart - 1];
				if (char.IsWhiteSpace(prev))
					break;
				tokenStart--;
			}

			string currentArgument = snapshot.GetText(tokenStart, curPos - tokenStart);

			// 如果当前正在输入的是新关键字，则不要误触发参数补全。
			if (currentArgument.StartsWith(".", StringComparison.Ordinal))
				return false;

			// 找当前参数前最近的关键字。
			int scan = tokenStart;
			while (scan > lineStart && char.IsWhiteSpace(snapshot[scan - 1]))
			{
				scan--;
			}

			int keywordEnd = scan;
			int keywordStart = keywordEnd;
			while (keywordStart > lineStart)
			{
				char prev = snapshot[keywordStart - 1];
				if (char.IsWhiteSpace(prev) || prev == ':')
					break;
				keywordStart--;
			}

			if (keywordEnd <= keywordStart)
				return false;

			string keyword = snapshot.GetText(keywordStart, keywordEnd - keywordStart);
			if (string.IsNullOrEmpty(keyword) || !keyword.StartsWith(".", StringComparison.Ordinal))
				return false;

			if (!DarkestEffectsData.KeywordToValuesMap.TryGetValue(keyword, out List<string> values) ||
				values == null ||
				values.Count == 0)
				return false;

			argumentStart = tokenStart;

			sourceList = FuzzyCompletionCache.GetMatches(
				values,
				currentArgument);

			return sourceList != null && sourceList.Count > 0;
		}

		public void Dispose()
		{
			_isDisposed = true;
		}
	}
}