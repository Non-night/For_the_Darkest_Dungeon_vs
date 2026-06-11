using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace For_the_Darkest_Dungeon.Completion
{
	[Export(typeof(ICompletionSourceProvider))]
	[ContentType("darkest-override")]
	[Name("Darkest Override Completion Provider")]
	internal sealed class OverrideCompletionSourceProvider : InfoBaseCompletionSourceProvider
	{
		protected override string KindName => "Override";
	}
}