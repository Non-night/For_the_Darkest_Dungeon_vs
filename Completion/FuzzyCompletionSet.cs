using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;

using VSCompletion = Microsoft.VisualStudio.Language.Intellisense.Completion;
using VSCompletionSet = Microsoft.VisualStudio.Language.Intellisense.CompletionSet;

namespace For_the_Darkest_Dungeon.Completion
{
	internal class FuzzyCompletionSet : VSCompletionSet
	{
		private readonly List<VSCompletion> _allCompletions;
		private readonly List<VSCompletion> _allCompletionBuilders;

		public FuzzyCompletionSet(
			string moniker,
			string displayName,
			ITrackingSpan applicableTo,
			IEnumerable<VSCompletion> completions,
			IEnumerable<VSCompletion> completionBuilders)
			: base(moniker, displayName, applicableTo, completions, completionBuilders)
		{
			_allCompletions = completions?.ToList() ?? new List<VSCompletion>();
			_allCompletionBuilders = completionBuilders?.ToList() ?? new List<VSCompletion>();
		}

		public override void SelectBestMatch()
		{
			base.SelectBestMatch();

			ForceSelectFirstCompletion();
		}

		public override void Recalculate()
		{
			base.Recalculate();
			ApplyFuzzyFilter();

			ForceSelectFirstCompletion();
		}

		private void ApplyFuzzyFilter()
		{
			string filterText = ApplicableTo.GetText(ApplicableTo.TextBuffer.CurrentSnapshot);

			if (string.IsNullOrWhiteSpace(filterText))
			{
				ResetWritableCollections(_allCompletions, _allCompletionBuilders);
				return;
			}

			List<string> matchedTexts = FuzzyCompletionCache.GetMatches(
				_allCompletions.Select(completion => completion.DisplayText).ToList(),
				filterText);

			HashSet<string> matchedTextSet = new HashSet<string>(matchedTexts, StringComparer.OrdinalIgnoreCase);
			List<VSCompletion> matchedCompletions = _allCompletions
				.Where(completion => matchedTextSet.Contains(completion.DisplayText))
				.OrderBy(completion => matchedTexts.FindIndex(text => string.Equals(text, completion.DisplayText, StringComparison.OrdinalIgnoreCase)))
				.ToList();

			ResetWritableCollections(matchedCompletions, _allCompletionBuilders);
		}

		private void ResetWritableCollections(
			IEnumerable<VSCompletion> completions,
			IEnumerable<VSCompletion> completionBuilders)
		{
			WritableCompletions.Clear();
			foreach (VSCompletion completion in completions)
			{
				WritableCompletions.Add(completion);
			}

			WritableCompletionBuilders.Clear();
			foreach (VSCompletion builder in completionBuilders)
			{
				WritableCompletionBuilders.Add(builder);
			}
		}

		private void ForceSelectFirstCompletion()
		{
			VSCompletion firstCompletion = Completions.FirstOrDefault();
			if (firstCompletion == null)
				return;

			// 过滤后需要始终重新选中第一项，
			// 不能依赖之前的 SelectionStatus，否则输入 . 后继续键入时容易出现“列表存在但未选中”的状态。
			SelectionStatus = new CompletionSelectionStatus(
				firstCompletion,
				true,
				Completions.Count == 1);
		}
	}
}