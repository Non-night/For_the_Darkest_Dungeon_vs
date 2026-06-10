using For_the_Darkest_Dungeon.Error;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace For_the_Darkest_Dungeon.Error
{
	/// <summary>
	/// Override 文件错误检查器。
	///
	/// 实际检查逻辑全部在 OverrideBaseErrorTagger 中。
	/// 这个类只负责把 darkest-override ContentType 绑定到公共错误检查逻辑。
	/// </summary>
	internal sealed class OverrideErrorTagger : InfoBaseErrorTagger
	{
		internal OverrideErrorTagger(ITextBuffer buffer)
			: base(buffer)
		{
		}
	}

	[Export(typeof(ITaggerProvider))]
	[ContentType("darkest-override")]
	[TagType(typeof(IErrorTag))]
	internal class OverrideErrorTaggerProvider : ITaggerProvider
	{
		public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
		{
			return buffer.Properties.GetOrCreateSingletonProperty(
				() => new OverrideErrorTagger(buffer)) as ITagger<T>;
		}
	}
}