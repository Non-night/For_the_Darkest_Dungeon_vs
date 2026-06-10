using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace For_the_Darkest_Dungeon.Classification
{
	/// <summary>
	/// Art 文件着色器。
	///
	/// 实际着色逻辑完全在 InfoBaseClassifier 中。
	/// 这里仅用于给 darkest-art ContentType 创建对应 Classifier 实例。
	/// </summary>
	internal sealed class ArtClassifier : InfoBaseClassifier
	{
		internal ArtClassifier(IClassificationTypeRegistryService registry)
			: base(registry)
		{
		}
	}

	[Export(typeof(IClassifierProvider))]
	[ContentType("darkest-art")]
	internal class ArtClassifierProvider : IClassifierProvider
	{
		[Import]
		internal IClassificationTypeRegistryService classificationRegistry;

		public IClassifier GetClassifier(ITextBuffer buffer)
		{
			return buffer.Properties.GetOrCreateSingletonProperty(
				() => new ArtClassifier(classificationRegistry));
		}
	}
}