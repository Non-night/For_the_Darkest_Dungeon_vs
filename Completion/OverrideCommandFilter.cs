using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text.Editor;

namespace For_the_Darkest_Dungeon.Completion
{
	/// <summary>
	/// Override 文件命令过滤器。
	///
	/// Override 文件与 Info / Art 文件在命令过滤与补全触发层面完全一致，
	/// 唯一差别只在于它挂接的文件类型不同。
	/// </summary>
	internal class OverrideCommandFilter : BaseSharedInfoLikeCommandFilter
	{
		public OverrideCommandFilter(IWpfTextView textView, ICompletionBroker broker)
			: base(textView, broker)
		{
		}

		/// <summary>
		/// Info / Art / Override 三类文件必须完全一致，
		/// 因此连续参数补全上下文统一以 Info 的规则为准。
		/// </summary>
		protected override bool IsInContinuousValueCompletionContext(string lineText)
		{
			return IsAfterAnyContinuousValueKeyword(
				lineText,
				".disabled_popup_text_types",
				".disabled_act_out_combat_start_turn_types");
		}
	}
}
