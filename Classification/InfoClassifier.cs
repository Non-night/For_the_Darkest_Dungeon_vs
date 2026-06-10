using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace For_the_Darkest_Dungeon.Classification
{
	/// <summary>
	/// Info 文件着色器。
	///
	/// 实际着色逻辑完全在 InfoBaseClassifier 中。
	/// 这里仅用于给 darkest-info ContentType 创建对应 Classifier 实例。
	/// </summary>
	internal sealed class InfoClassifier : InfoBaseClassifier
	{
		internal InfoClassifier(IClassificationTypeRegistryService registry)
			: base(registry)
		{
		}
	}

	[Export(typeof(IClassifierProvider))]
	[ContentType("darkest-info")]
	internal class InfoClassifierProvider : IClassifierProvider
	{
		[Import]
		internal IClassificationTypeRegistryService classificationRegistry;

		public IClassifier GetClassifier(ITextBuffer buffer)
		{
			return buffer.Properties.GetOrCreateSingletonProperty(
				() => new InfoClassifier(classificationRegistry));
		}
	}
}