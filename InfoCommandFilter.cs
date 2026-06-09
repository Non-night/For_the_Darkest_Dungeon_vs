using For_the_Darkest_Dungeon.DefinitionDarkest;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

internal class InfoCommandFilter : IOleCommandTarget
{
	private ICompletionSession _currentSession;

	public InfoCommandFilter(IWpfTextView textView, ICompletionBroker broker)
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

		if (pguidCmdGroup == VSConstants.VSStd2K)
		{
			switch ((VSConstants.VSStd2KCmdID)nCmdID)
			{
				case VSConstants.VSStd2KCmdID.TAB:
				case VSConstants.VSStd2KCmdID.RETURN:
					if (_currentSession != null && !_currentSession.IsDismissed)
					{
						//ForceSelectCompletion();

						if (_currentSession.SelectedCompletionSet != null &&
							_currentSession.SelectedCompletionSet.SelectionStatus.IsSelected)
						{
							_currentSession.Commit();
							return VSConstants.S_OK;
						}
						else
						{
							_currentSession.Dismiss();
						}
					}
					break;

				case VSConstants.VSStd2KCmdID.TYPECHAR:
					return HandleTypeChar(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
			}
		}

		return Next.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
	}

	private int HandleTypeChar(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
	{
		ThreadHelper.ThrowIfNotOnUIThread();

		char typedChar = (char)(ushort)Marshal.GetObjectForNativeVariant(pvaIn);

		int retVal = Next.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);

		var caretPos = TextView.Caret.Position.BufferPosition;
		var snapshot = TextView.TextSnapshot;
		var line = caretPos.GetContainingLine();
		string lineText = snapshot.GetText(line.Start, caretPos.Position - line.Start);
		string trimmedText = lineText.TrimEnd();

		if (char.IsLetter(typedChar) && !lineText.Contains(":") && typedChar != '.' && typedChar != ' '
			&& DarkestInfoData.AllHeaders.Any(h => h.StartsWith(lineText, StringComparison.OrdinalIgnoreCase)))
		{
			TriggerCompletion();
		}
		if (typedChar == '.')
		{
			int dotPos = caretPos.Position - 1;

			if (dotPos >= line.Start.Position && IsDotAtValidKeywordStart(snapshot, dotPos))
			{
				// Info 文件里合法位置的 . 开头是关键字，触发补全
				TriggerCompletion();
			}
			else if (_currentSession != null && !_currentSession.IsDismissed)
			{
				_currentSession.Dismiss();
			}
		}
		else if (typedChar == ' ')
		{
			bool shouldTrigger = DarkestInfoData.InfoContextMap.Values
				.Any(keywords => keywords.Any(kw => trimmedText.EndsWith(kw)));

			string lastWord = trimmedText.Split(' ', '\t', '\n').LastOrDefault();

			bool isDisabledPopupTextTypesContext =
				IsAfterDisabledPopupTextTypesKeyword(lineText);

			if ((shouldTrigger && DarkestInfoData.IsKeywordHasStaticValues(lastWord))
				|| isDisabledPopupTextTypesContext)
			{
				TriggerCompletion();
			}
		}
		else if (char.IsLetterOrDigit(typedChar) || typedChar == '_')
		{
			string currentToken;

			if (TryGetValidDotKeywordToken(snapshot, caretPos, out currentToken))
			{
				if (currentToken.Length <= 2 && _currentSession != null && !_currentSession.IsDismissed)
				{
					_currentSession.Filter();
				}
				else
				{
					TriggerCompletion();
				}
			}
			else if (IsAfterDisabledPopupTextTypesKeyword(lineText))
			{
				// .disabled_popup_text_types 的连续参数补全仍然保留
				TriggerCompletion();
			}
			else if (_currentSession != null && !_currentSession.IsDismissed)
			{
				// 其他普通参数补全继续走默认过滤，避免破坏已有功能
				_currentSession.Filter();
			}
		}
		else if (_currentSession != null && !_currentSession.IsDismissed)
		{
			_currentSession.Filter();
		}

		return retVal;
	}

	private void TriggerCompletion()
	{
		ITextSnapshot snapshot = TextView.TextSnapshot;
		SnapshotPoint caret = TextView.Caret.Position.BufferPosition;

		if (_currentSession != null && !_currentSession.IsDismissed)
			_currentSession.Dismiss();

		ITrackingPoint triggerPoint = snapshot.CreateTrackingPoint(
			caret.Position, PointTrackingMode.Positive);

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

	private bool IsAfterDisabledPopupTextTypesKeyword(string lineText)
	{
		const string keyword = ".disabled_popup_text_types";

		int keywordIndex = lineText.LastIndexOf(keyword, StringComparison.Ordinal);
		if (keywordIndex < 0)
			return false;

		int afterKeywordIndex = keywordIndex + keyword.Length;

		// 必须已经进入参数区域，避免影响正在输入 .disabled_popup_text_types 本身。
		if (afterKeywordIndex >= lineText.Length)
			return false;

		return char.IsWhiteSpace(lineText[afterKeywordIndex]);
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