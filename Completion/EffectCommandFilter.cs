using For_the_Darkest_Dungeon.DefinitionDarkest;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System.Linq;

namespace For_the_Darkest_Dungeon.Completion
{
	/// <summary>
	/// Effect 文件专用命令过滤器。
	///
	/// 与 Info / Art / Override 不同，Effect 文件不需要 Header 补全；
	/// 但它在普通参数输入阶段，需要保留“直接重新触发补全”的旧行为。
	/// </summary>
	internal class EffectCommandFilter : BaseDarkestCommandFilter
	{
		public EffectCommandFilter(IWpfTextView textView, ICompletionBroker broker)
			: base(textView, broker)
		{
		}

		/// <summary>
		/// Effect 文件在空格后，如果前一个关键字存在静态参数候选，就立刻触发参数补全。
		/// </summary>
		protected override bool ShouldTriggerCompletionOnSpace(string lineText, string trimmedText)
		{
			return DarkestEffectsData.KeywordToValuesMap.Keys.Any(keyword => trimmedText.EndsWith(keyword));
		}

		/// <summary>
		/// Effect 文件中，如果当前输入不在合法的 .关键字 token 中，
		/// 但补全窗口已经存在，则继续强制重新触发补全，保留原有参数补全行为。
		/// </summary>
		protected override void HandleNonDotTokenWordChar(
			ITextSnapshot snapshot,
			SnapshotPoint caretPos,
			string lineText,
			string trimmedText)
		{
			if (HasActiveSession())
			{
				TriggerCompletion();
			}
		}
	}
}
