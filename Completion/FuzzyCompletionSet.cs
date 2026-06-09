using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Linq;

using VSCompletion = Microsoft.VisualStudio.Language.Intellisense.Completion;
using VSCompletionSet = Microsoft.VisualStudio.Language.Intellisense.CompletionSet;

namespace For_the_Darkest_Dungeon.Completion
{
	internal class FuzzyCompletionSet : VSCompletionSet
	{
		public FuzzyCompletionSet(
			string moniker,
			string displayName,
			ITrackingSpan applicableTo,
			IEnumerable<VSCompletion> completions,
			IEnumerable<VSCompletion> completionBuilders)
			: base(moniker, displayName, applicableTo, completions, completionBuilders)
		{
		}

		public override void SelectBestMatch()
		{
			base.SelectBestMatch();

			ForceSelectFirstCompletion();
		}

		public override void Recalculate()
		{
			base.Recalculate();

			ForceSelectFirstCompletion();
		}

		private void ForceSelectFirstCompletion()
		{
			if (SelectionStatus.IsSelected)
				return;

			VSCompletion firstCompletion = Completions.FirstOrDefault();
			if (firstCompletion == null)
				return;

			SelectionStatus = new CompletionSelectionStatus(
				firstCompletion,
				true,
				Completions.Count == 1);
		}
	}
}