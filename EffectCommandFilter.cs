using For_the_Darkest_Dungeon.DefinitionDarkest;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace For_the_Darkest_Dungeon.Completion
{
	internal class EffectCommandFilter : IOleCommandTarget
	{
		private ICompletionSession _currentSession;

		public EffectCommandFilter(IWpfTextView textView, ICompletionBroker broker)
		{
			_currentSession = null;
			TextView = textView;
			Broker = broker;
		}

		public IWpfTextView TextView { get; private set; }
		public ICompletionBroker Broker { get; private set; }
		public IOleCommandTarget Next { get; set; }

		public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			// 1. 处理“提交”按键（Tab 或 Enter）
			if (pguidCmdGroup == VSConstants.VSStd2K)
			{
				switch ((VSConstants.VSStd2KCmdID)nCmdID)
				{
					case VSConstants.VSStd2KCmdID.TAB:
					case VSConstants.VSStd2KCmdID.RETURN:
						// 如果当前补全窗口开着，则尝试提交当前选中的项
						if (_currentSession != null && !_currentSession.IsDismissed)
						{
							//ForceSelectCompletion();

							// 如果已经有匹配的项或者用户手动选中了项，Commit 会将其填入编辑器
							if (_currentSession.SelectedCompletionSet != null &&
								_currentSession.SelectedCompletionSet.SelectionStatus.IsSelected)
							{
								_currentSession.Commit();
								return VSConstants.S_OK; // 拦截按键，不再传递给编辑器（防止回车换行）
							}
							else
							{
								_currentSession.Dismiss();
							}
						}
						break;
					case VSConstants.VSStd2KCmdID.TYPECHAR:
						// 这是你原有的处理输入字符的逻辑
						return HandleTypeChar(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
				}
			}

			return Next.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
		}

		// 建议将字符输入逻辑拆分出来，保持代码清晰
		private int HandleTypeChar(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			char typedChar = (char)(ushort)Marshal.GetObjectForNativeVariant(pvaIn);

			// 先让字符上屏
			int retVal = Next.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);

			var caretPos = TextView.Caret.Position.BufferPosition;
			var snapshot = TextView.TextSnapshot;
			var line = caretPos.GetContainingLine();
			string lineText = snapshot.GetText(line.Start, caretPos.Position - line.Start);
			string trimmedText = lineText.TrimEnd();

			if (typedChar == '.')
			{
				int dotPos = caretPos.Position - 1;

				if (dotPos >= line.Start.Position && IsDotAtValidKeywordStart(snapshot, dotPos))
				{
					TriggerCompletion();
				}
				else if (_currentSession != null && !_currentSession.IsDismissed)
				{
					_currentSession.Dismiss();
				}
			}
			else if (typedChar == ' ')
			{
				if (DarkestEffectsData.KeywordToValuesMap.Keys.Any(k => trimmedText.EndsWith(k))
					/*trimmedText.EndsWith(".target") || trimmedText.EndsWith(".curio_result_type") || trimmedText.EndsWith(".keyStatus")*/)
				{
					TriggerCompletion();
				}
			}
			else if (char.IsLetterOrDigit(typedChar) || typedChar == '_')
			{
				string currentToken;

				if (TryGetValidDotKeywordToken(snapshot, caretPos, out currentToken))
				{
					// 短输入时用 VS 自带 Filter，响应更快
					// 输入到 3 个字符左右，再重新触发模糊匹配
					if (currentToken.Length <= 2 && _currentSession != null && !_currentSession.IsDismissed)
					{
						_currentSession.Filter();
					}
					else
					{
						TriggerCompletion();
					}
				}
				else if (_currentSession != null && !_currentSession.IsDismissed)
				{
					// 当前 token 不是合法的 .关键字 token。
					// 这里保留你已有的参数补全逻辑：非 . token 可能是在输入关键字后的参数。
					TriggerCompletion();
				}
			}
			else if (_currentSession != null && !_currentSession.IsDismissed)
			{
				// 其他字符仍然交给 VS 默认过滤
				_currentSession.Filter();
			}

			return retVal;
		}

		private void TriggerCompletion()
		{
			// 使用当前 Caret 的 Position 即可，不需要复杂的 GetPoint 过滤，
			// 除非你是在处理非常复杂的投影缓冲区（Projection Buffer）
			ITextSnapshot snapshot = TextView.TextSnapshot;
			SnapshotPoint caret = TextView.Caret.Position.BufferPosition;

			if (_currentSession != null && !_currentSession.IsDismissed)
				_currentSession.Dismiss(); // 如果有旧的会话，先关闭

			// 创建跟踪点，PointTrackingMode.Positive 确保补全窗口跟随新输入的字符
			ITrackingPoint triggerPoint = snapshot.CreateTrackingPoint(caret.Position, PointTrackingMode.Positive);

			_currentSession = Broker.CreateCompletionSession(TextView, triggerPoint, true);

			if (_currentSession != null)
			{
				_currentSession.Dismissed += (sender, args) => _currentSession = null;
				_currentSession.Start();
				ForceSelectCompletion();
			}
		}

		private string GetCurrentToken(ITextSnapshot snapshot, SnapshotPoint caret)
		{
			var line = caret.GetContainingLine();
			int start = caret.Position;

			while (start > line.Start.Position)
			{
				char prevChar = snapshot[start - 1];

				if (char.IsWhiteSpace(prevChar) || prevChar == ':')
					break;

				start--;
			}

			return snapshot.GetText(start, caret.Position - start);
		}

		private void ForceSelectCompletion()
		{
			if (_currentSession == null || _currentSession.IsDismissed)
				return;

			var completionSet = _currentSession.SelectedCompletionSet;
			if (completionSet == null || completionSet.Completions.Count == 0)
				return;

			completionSet.SelectBestMatch();
			completionSet.Recalculate();
		}

		private bool IsDotAtValidKeywordStart(ITextSnapshot snapshot, int dotPosition)
		{
			var line = snapshot.GetLineFromPosition(dotPosition);

			if (dotPosition == line.Start.Position)
				return true;

			char prevChar = snapshot[dotPosition - 1];

			return char.IsWhiteSpace(prevChar);
		}

		private bool TryGetValidDotKeywordToken(ITextSnapshot snapshot, SnapshotPoint caret, out string currentToken)
		{
			currentToken = GetCurrentToken(snapshot, caret);

			if (string.IsNullOrEmpty(currentToken))
				return false;

			// 必须是当前 token 的第一个字符是 .
			if (!currentToken.StartsWith("."))
				return false;

			// 不允许 .hp. 这种 token 内出现第二个点
			if (currentToken.IndexOf('.', 1) >= 0)
				return false;

			var line = caret.GetContainingLine();
			int tokenStart = caret.Position - currentToken.Length;

			if (tokenStart == line.Start.Position)
				return true;

			char prevChar = snapshot[tokenStart - 1];

			return char.IsWhiteSpace(prevChar);
		}

		public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			return Next.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
		}
	}
}