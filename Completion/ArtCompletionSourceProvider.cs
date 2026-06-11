using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace For_the_Darkest_Dungeon.Completion
{
	[Export(typeof(ICompletionSourceProvider))]
	[ContentType("darkest-art")]
	[Name("Darkest Art Completion Provider")]
	internal sealed class ArtCompletionSourceProvider : InfoBaseCompletionSourceProvider
	{
		protected override string KindName => "Art";
	}
}