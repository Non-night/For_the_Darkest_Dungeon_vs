using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Runtime.InteropServices;

namespace For_the_Darkest_Dungeon.Completion
{
	/// <summary>
	/// Darkest 系列文件补全命令过滤器基类。
	///
	/// 这个基类负责统一处理以下公共流程：
	/// 1. Tab / Enter 提交补全；
	/// 2. TYPECHAR 输入后的通用补全调度；
	/// 3. 补全会话的创建、关闭、过滤与最佳项选中；
	/// 4. .关键字 token 的公共判定。
	///
	/// 子类只需要覆写各自的差异化策略：
	/// - 是否支持 Header 补全；
	/// - 空格后是否触发补全；
	/// - 非 .关键字 token 输入时如何处理；
	/// - 是否处于连续参数补全上下文。
	/// </summary>
	internal abstract class BaseDarkestCommandFilter : IOleCommandTarget
	{
		/// <summary>
		/// 当前激活的补全会话。
		/// </summary>
		private ICompletionSession _currentSession;

		protected BaseDarkestCommandFilter(IWpfTextView textView, ICompletionBroker broker)
		{
			_currentSession = null;
			TextView = textView;
			Broker = broker;
		}

		/// <summary>
		/// 当前文本视图。
		/// </summary>
		protected IWpfTextView TextView { get; }

		/// <summary>
		/// VS 补全代理。
		/// </summary>
		protected ICompletionBroker Broker { get; }

		/// <summary>
		/// 命令链中的下一个目标。
		/// </summary>
		public IOleCommandTarget Next { get; set; }

		/// <summary>
		/// 当前是否支持 Header 补全。
		/// Effect 文件不需要，Info / Art / Override 需要。
		/// </summary>
		protected virtual bool SupportsHeaderCompletion => false;

		public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			if (pguidCmdGroup == VSConstants.VSStd2K)
			{
				switch ((VSConstants.VSStd2KCmdID)nCmdID)
				{
					case VSConstants.VSStd2KCmdID.TAB:
					case VSConstants.VSStd2KCmdID.RETURN:
						return HandleCommitOrDismiss(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);

					case VSConstants.VSStd2KCmdID.TYPECHAR:
						return HandleTypeChar(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
				}
			}

			return Next.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
		}

		/// <summary>
		/// 处理 Tab / Enter：若当前补全项已选中，则提交；否则关闭补全并继续传递命令。
		/// </summary>
		private int HandleCommitOrDismiss(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
		{
			if (HasActiveSession())
			{
				if (_currentSession.SelectedCompletionSet != null &&
					_currentSession.SelectedCompletionSet.SelectionStatus.IsSelected)
				{
					_currentSession.Commit();
					return VSConstants.S_OK;
				}

				_currentSession.Dismiss();
			}

			return Next.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
		}

		/// <summary>
		/// 统一处理字符输入。
		/// 这里先让字符上屏，再根据子类策略决定是否触发 / 过滤 / 关闭补全。
		/// </summary>
		private int HandleTypeChar(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			char typedChar = (char)(ushort)Marshal.GetObjectForNativeVariant(pvaIn);

			// 先让字符真正写入编辑器，再读取最新光标上下文。
			int retVal = Next.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);

			SnapshotPoint caretPos = TextView.Caret.Position.BufferPosition;
			ITextSnapshot snapshot = TextView.TextSnapshot;
			ITextSnapshotLine line = caretPos.GetContainingLine();
			string lineText = snapshot.GetText(line.Start, caretPos.Position - line.Start);
			string trimmedText = lineText.TrimEnd();

			if (SupportsHeaderCompletion && ShouldTriggerHeaderCompletion(typedChar, lineText))
			{
				TriggerCompletion();
			}

			if (typedChar == '.')
			{
				HandleDotChar(snapshot, line, caretPos);
			}
			else if (typedChar == ' ')
			{
				if (ShouldTriggerCompletionOnSpace(lineText, trimmedText))
				{
					TriggerCompletion();
				}
			}
			else if (char.IsLetterOrDigit(typedChar) || typedChar == '_')
			{
				HandleWordChar(snapshot, caretPos, lineText, trimmedText);
			}
			else if (HasActiveSession())
			{
				// 其他字符保留 VS 默认过滤行为。
				_currentSession.Filter();
			}

			return retVal;
		}

		/// <summary>
		/// 处理 . 输入。
		/// 合法位置触发补全，非法位置则关闭当前补全。
		/// </summary>
		private void HandleDotChar(ITextSnapshot snapshot, ITextSnapshotLine line, SnapshotPoint caretPos)
		{
			int dotPos = caretPos.Position - 1;

			if (dotPos < line.Start.Position)
			{
				return;
			}

			if (IsDotAtValidKeywordStart(snapshot, dotPos))
			{
				TriggerCompletion();
			}
			else if (HasActiveSession())
			{
				_currentSession.Dismiss();
			}
		}

		/// <summary>
		/// 处理普通单词字符。
		/// 若当前 token 是合法 .关键字，则按关键字补全逻辑处理；
		/// 否则交由子类处理其自定义上下文。
		/// </summary>
		private void HandleWordChar(ITextSnapshot snapshot, SnapshotPoint caretPos, string lineText, string trimmedText)
		{
			if (TryGetValidDotKeywordToken(snapshot, caretPos, out string currentToken))
			{
				if (currentToken.Length > 1)
				{
					if (!HasActiveSession())
					{
						TriggerCompletion();
					}
					else
					{
						FilterActiveSession();
						ForceSelectCompletion();
					}
				}

				return;
			}

			HandleNonDotTokenWordChar(snapshot, caretPos, lineText, trimmedText);
		}

		/// <summary>
		/// 判断是否应该触发 Header 补全。
		/// 默认：仅当当前行以 identifier: 形式结束时返回 true。
		/// </summary>
		protected virtual bool ShouldTriggerHeaderCompletion(char typedChar, string lineText)
		{
			return typedChar == ':' && SupportsHeaderCompletion;
		}

		/// <summary>
		/// 处理非 .关键字 token 输入。
		/// 子类可按自身参数上下文决定是否触发 / 过滤 / 关闭补全。
		/// </summary>
		protected abstract void HandleNonDotTokenWordChar(
			ITextSnapshot snapshot,
			SnapshotPoint caretPos,
			string lineText,
			string trimmedText);

		/// <summary>
		/// 处理空格输入后是否触发补全。
		/// 子类按关键字语义覆写。
		/// </summary>
		protected abstract bool ShouldTriggerCompletionOnSpace(string lineText, string trimmedText);

		/// <summary>
		/// 判断是否处于连续参数补全的上下文中。
		/// 默认返回 false，只有需要连续参数补全的子类才覆写。
		/// </summary>
		protected virtual bool IsInContinuousValueCompletionContext(string lineText)
		{
			return false;
		}

		/// <summary>
		/// 创建并启动一个新的补全会话。
		/// 若旧会话仍存在，则先关闭旧会话。
		/// </summary>
		protected void TriggerCompletion()
		{
			ITextSnapshot snapshot = TextView.TextSnapshot;
			SnapshotPoint caret = TextView.Caret.Position.BufferPosition;

			if (HasActiveSession())
			{
				_currentSession.Dismiss();
			}

			ITrackingPoint triggerPoint = snapshot.CreateTrackingPoint(
				caret.Position,
				PointTrackingMode.Positive);

			_currentSession = Broker.CreateCompletionSession(TextView, triggerPoint, true);

			if (_currentSession != null)
			{
				_currentSession.Dismissed += (sender, args) => _currentSession = null;
				_currentSession.Start();
				ForceSelectCompletion();
			}
		}

		/// <summary>
		/// 当前是否存在未关闭的补全会话。
		/// </summary>
		protected bool HasActiveSession()
		{
			return _currentSession != null && !_currentSession.IsDismissed;
		}

		/// <summary>
		/// 对当前补全会话执行默认过滤。
		/// 若当前没有会话，则不做任何事。
		/// </summary>
		protected void FilterActiveSession()
		{
			if (HasActiveSession())
			{
				_currentSession.Filter();
			}
		}

		/// <summary>
		/// 获取光标所在 token。
		/// token 以空白或冒号为边界。
		/// </summary>
		protected string GetCurrentToken(ITextSnapshot snapshot, SnapshotPoint caret)
		{
			ITextSnapshotLine line = caret.GetContainingLine();
			int start = caret.Position;

			while (start > line.Start.Position)
			{
				char prevChar = snapshot[start - 1];

				if (char.IsWhiteSpace(prevChar) || prevChar == ':')
				{
					break;
				}

				start--;
			}

			return snapshot.GetText(start, caret.Position - start);
		}

		/// <summary>
		/// 尝试强制选中当前补全集中的最佳匹配项。
		/// 这样在用户按 Tab / Enter 时能直接提交最佳项。
		/// </summary>
		protected void ForceSelectCompletion()
		{
			if (!HasActiveSession())
			{
				return;
			}

			var completionSet = _currentSession.SelectedCompletionSet;
			if (completionSet == null || completionSet.Completions.Count == 0)
			{
				return;
			}

			completionSet.SelectBestMatch();
			completionSet.Recalculate();
		}

		/// <summary>
		/// 判断某个 . 是否出现在合法关键字起始位置。
		/// 规则：位于行首，或前一个字符为空白。
		/// </summary>
		protected bool IsDotAtValidKeywordStart(ITextSnapshot snapshot, int dotPosition)
		{
			ITextSnapshotLine line = snapshot.GetLineFromPosition(dotPosition);

			if (dotPosition == line.Start.Position)
			{
				return true;
			}

			char prevChar = snapshot[dotPosition - 1];
			return char.IsWhiteSpace(prevChar);
		}

		/// <summary>
		/// 判断当前 token 是否是一个合法的 .关键字 token。
		/// 规则：
		/// 1. 必须以 . 开头；
		/// 2. token 内不能出现第二个点；
		/// 3. token 起始位置必须在行首或空白后。
		/// </summary>
		protected bool TryGetValidDotKeywordToken(ITextSnapshot snapshot, SnapshotPoint caret, out string currentToken)
		{
			currentToken = GetCurrentToken(snapshot, caret);

			if (string.IsNullOrEmpty(currentToken))
			{
				return false;
			}

			if (!currentToken.StartsWith("."))
			{
				return false;
			}

			if (currentToken.IndexOf('.', 1) >= 0)
			{
				return false;
			}

			ITextSnapshotLine line = caret.GetContainingLine();
			int tokenStart = caret.Position - currentToken.Length;

			if (tokenStart == line.Start.Position)
			{
				return true;
			}

			char prevChar = snapshot[tokenStart - 1];
			return char.IsWhiteSpace(prevChar);
		}

		/// <summary>
		/// 公共连续参数上下文判断辅助函数。
		/// 只要关键字已经出现，且后面已经进入参数区域，就返回 true。
		/// </summary>
		protected bool IsAfterAnyContinuousValueKeyword(string lineText, params string[] keywords)
		{
			foreach (string keyword in keywords)
			{
				int keywordIndex = lineText.LastIndexOf(keyword, StringComparison.Ordinal);
				if (keywordIndex < 0)
				{
					continue;
				}

				int afterKeywordIndex = keywordIndex + keyword.Length;
				if (afterKeywordIndex >= lineText.Length)
				{
					continue;
				}

				if (char.IsWhiteSpace(lineText[afterKeywordIndex]))
				{
					return true;
				}
			}

			return false;
		}

		public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			return Next.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
		}
	}
}