using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace For_the_Darkest_Dungeon.Completion
{
	[Export(typeof(ICompletionSourceProvider))]
	[ContentType("darkest-info")]
	[Name("Darkest Info Completion Provider")]
	internal sealed class InfoCompletionSourceProvider : InfoBaseCompletionSourceProvider
	{
		protected override string KindName => "Info";
	}
}