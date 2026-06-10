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

				// 使用模糊匹配生成候选列表
				// 例如 ".awr" 可以匹配 ".apply_with_result"
				sourceList = FuzzyCompletionCache.GetMatches(
					DarkestEffectsData.AllKeywords,
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
			// 例如 ".target e" 中，当前 token 是 "e"，argumentStart 指向 e。
			int tokenStart = curPos;
			while (tokenStart > lineStart)
			{
				char prevChar = snapshot[tokenStart - 1];

				if (char.IsWhiteSpace(prevChar))
					break;

				tokenStart--;
			}

			string currentArgument = snapshot.GetText(tokenStart, curPos - tokenStart);

			// 找当前参数前面的关键字。
			// 例如 ".target e" 中，从 e 前面的空格继续往前找 ".target"。
			int keywordEnd = tokenStart;

			while (keywordEnd > lineStart && char.IsWhiteSpace(snapshot[keywordEnd - 1]))
				keywordEnd--;

			if (keywordEnd <= lineStart)
				return false;

			int keywordStart = keywordEnd;
			while (keywordStart > lineStart)
			{
				char prevChar = snapshot[keywordStart - 1];

				if (char.IsWhiteSpace(prevChar))
					break;

				keywordStart--;
			}

			string keyword = snapshot.GetText(keywordStart, keywordEnd - keywordStart);

			if (string.IsNullOrEmpty(keyword))
				return false;

			if (!DarkestEffectsData.KeywordToValuesMap.TryGetValue(keyword, out var values))
				return false;

			argumentStart = tokenStart;

			sourceList = FuzzyCompletionCache.GetMatches(
				values,
				currentArgument);

			return sourceList != null && sourceList.Count > 0;
		}

		public void Dispose() => _isDisposed = true;
	}
}