using For_the_Darkest_Dungeon.DefinitionDarkest;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Linq;

namespace For_the_Darkest_Dungeon.Completion
{
	/// <summary>
	/// Info / Art / Override 三类文件共用的命令过滤器基类。
	///
	/// 这三类文件的补全交互逻辑必须完全一致，
	/// 因此把它们的共同策略集中在这里，避免以后再次发生逻辑漂移。
	/// </summary>
	internal abstract class BaseSharedInfoLikeCommandFilter : BaseDarkestCommandFilter
	{
		protected BaseSharedInfoLikeCommandFilter(IWpfTextView textView, ICompletionBroker broker)
			: base(textView, broker)
		{
		}

		/// <summary>
		/// Info / Art / Override 都支持 Header 补全。
		/// </summary>
		protected override bool SupportsHeaderCompletion => true;

		/// <summary>
		/// 当用户正在输入 Header 且当前行还没有冒号时，触发 Header 补全。
		/// 例如输入 skill、buff、actor 等前缀时给出候选。
		/// </summary>
		protected override bool ShouldTriggerHeaderCompletion(char typedChar, string lineText)
		{
			return char.IsLetter(typedChar)
				&& !lineText.Contains(":")
				&& typedChar != '.'
				&& typedChar != ' '
				&& DarkestInfoData.AllHeaders.Any(header =>
					header.StartsWith(lineText, StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>
		/// 空格后触发补全的规则：
		/// 1. 当前关键字具有静态候选值；
		/// 2. 或者已经进入连续参数补全上下文。
		/// </summary>
		protected override bool ShouldTriggerCompletionOnSpace(string lineText, string trimmedText)
		{
			bool shouldTrigger = DarkestInfoData.InfoContextMap.Values
				.Any(keywords => keywords.Any(keyword => trimmedText.EndsWith(keyword)));

			string lastWord = trimmedText.Split(' ', '\t', '\n').LastOrDefault();
			bool isContinuousContext = IsInContinuousValueCompletionContext(lineText);

			return (shouldTrigger && DarkestInfoData.IsKeywordHasStaticValues(lastWord))
				|| isContinuousContext;
		}

		/// <summary>
		/// 对于 Info / Art / Override：
		/// 1. 如果处于连续参数补全上下文，则重新触发补全；
		/// 2. 否则若补全窗口已存在，则仅执行默认过滤。
		/// 这样可以完整保留三者原有的行为。
		/// </summary>
		protected override void HandleNonDotTokenWordChar(
			ITextSnapshot snapshot,
			SnapshotPoint caretPos,
			string lineText,
			string trimmedText)
		{
			if (IsInContinuousValueCompletionContext(lineText))
			{
				TriggerCompletion();
			}
			else
			{
				FilterActiveSession();
			}
		}
	}
}
