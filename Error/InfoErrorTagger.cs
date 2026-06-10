using For_the_Darkest_Dungeon.Error;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace For_the_Darkest_Dungeon.Error
{
	/// <summary>
	/// Info 文件错误检查器。
	///
	/// 实际检查逻辑全部在 InfoBaseErrorTagger 中。
	/// 这个类只负责把 darkest-info ContentType 绑定到公共错误检查逻辑。
	/// </summary>
	internal sealed class InfoErrorTagger : InfoBaseErrorTagger
	{
		internal InfoErrorTagger(ITextBuffer buffer)
			: base(buffer)
		{
		}
	}

	[Export(typeof(ITaggerProvider))]
	[ContentType("darkest-info")]
	[TagType(typeof(IErrorTag))]
	internal class InfoErrorTaggerProvider : ITaggerProvider
	{
		public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
		{
			return buffer.Properties.GetOrCreateSingletonProperty(
				() => new InfoErrorTagger(buffer)) as ITagger<T>;
		}
	}
}