using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace For_the_Darkest_Dungeon.Completion
{
    /// <summary>
    /// 为所有 *.darkest 编辑器提供 Ctrl+/ 多行快捷注释功能。
    /// </summary>
    internal sealed class DarkestToggleCommentCommandFilter : IOleCommandTarget
    {
        private readonly IWpfTextView _textView;
        private readonly ITextUndoHistoryRegistry _undoHistoryRegistry;
        private readonly For_the_Darkest_DungeonPackage _package;

        internal DarkestToggleCommentCommandFilter(
            IWpfTextView textView,
            ITextUndoHistoryRegistry undoHistoryRegistry,
            For_the_Darkest_DungeonPackage package)
        {
            _textView = textView;
            _undoHistoryRegistry = undoHistoryRegistry;
            _package = package;
        }

        public IOleCommandTarget Next { get; set; }

        /// <summary>
        /// 绑定主键盘 Ctrl + /? 的按键处理。
        /// </summary>
        internal void AttachKeyHandler()
        {
            _textView.VisualElement.PreviewKeyDown -= OnPreviewKeyDown;
            _textView.VisualElement.PreviewKeyDown += OnPreviewKeyDown;
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return Next.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return Next.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        /// <summary>
        /// 只处理主键盘的 Ctrl + /? 键，不处理小键盘。
        /// </summary>
        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
            {
                return;
            }

            // 只判断主键盘 /? 键。
            if (e.Key != Key.OemQuestion)
            {
                return;
            }

            if (!IsToggleCommentEnabled())
            {
                return;
            }

            if (TryToggleSelectedLinesComment())
            {
                e.Handled = true;
            }
        }

        /// <summary>
        /// 读取设置页中的快捷注释开关，允许用户自行启用或关闭该功能。
        /// </summary>
        private bool IsToggleCommentEnabled()
        {
            if (_package == null)
            {
                return true;
            }

            var optionsPage = _package.GetDialogPage(typeof(GeneralOptionsPage)) as GeneralOptionsPage;
            return optionsPage?.EnableCtrlSlashToggleComment ?? true;
        }

        /// <summary>
        /// 对当前选中的所有非空行执行“整行加 // / 去 //”切换。
        /// 空行会被跳过，不参与注释状态判定，也不会被修改。
        /// </summary>
        internal bool TryToggleSelectedLinesComment()
        {
            ITextSnapshot snapshot = _textView.TextSnapshot;
            var selection = _textView.Selection;

            int startLineNumber = selection.IsEmpty
                ? _textView.Caret.Position.BufferPosition.GetContainingLine().LineNumber
                : selection.Start.Position.GetContainingLine().LineNumber;

            int endLineNumber = selection.IsEmpty
                ? _textView.Caret.Position.BufferPosition.GetContainingLine().LineNumber
                : selection.End.Position.GetContainingLine().LineNumber;

            if (!selection.IsEmpty && selection.End.Position.Position > selection.Start.Position.Position)
            {
                ITextSnapshotLine endLine = selection.End.Position.GetContainingLine();
                if (selection.End.Position.Position == endLine.Start.Position && endLineNumber > startLineNumber)
                {
                    endLineNumber--;
                }
            }

            if (endLineNumber < startLineNumber)
            {
                endLineNumber = startLineNumber;
            }

            bool hasNonEmptyLine = false;
            bool allNonEmptyLinesCommented = true;

            for (int lineNumber = startLineNumber; lineNumber <= endLineNumber; lineNumber++)
            {
                ITextSnapshotLine line = snapshot.GetLineFromLineNumber(lineNumber);
                string lineText = line.GetText();

                if (string.IsNullOrWhiteSpace(lineText))
                {
                    continue;
                }

                hasNonEmptyLine = true;
                if (!IsLineCommented(lineText))
                {
                    allNonEmptyLinesCommented = false;
                    break;
                }
            }

            if (!hasNonEmptyLine)
            {
                return true;
            }

            using (var undoTransaction = _undoHistoryRegistry.RegisterHistory(_textView.TextBuffer).CreateTransaction("Darkest 切换注释"))
            using (ITextEdit edit = _textView.TextBuffer.CreateEdit())
            {
                for (int lineNumber = startLineNumber; lineNumber <= endLineNumber; lineNumber++)
                {
                    ITextSnapshotLine line = snapshot.GetLineFromLineNumber(lineNumber);
                    string lineText = line.GetText();

                    if (string.IsNullOrWhiteSpace(lineText))
                    {
                        continue;
                    }

                    int firstNonWhitespaceIndex = GetFirstNonWhitespaceIndex(lineText);
                    if (firstNonWhitespaceIndex < 0)
                    {
                        continue;
                    }

                    int insertOrRemovePosition = line.Start.Position + firstNonWhitespaceIndex;
                    if (allNonEmptyLinesCommented)
                    {
                        edit.Delete(insertOrRemovePosition, 2);
                    }
                    else
                    {
                        edit.Insert(insertOrRemovePosition, "//");
                    }
                }

                edit.Apply();
                undoTransaction.Complete();
            }

            return true;
        }

        /// <summary>
        /// 判断一行是否以 // 注释开头，前面允许存在空格或制表符。
        /// </summary>
        private static bool IsLineCommented(string lineText)
        {
            int firstNonWhitespaceIndex = GetFirstNonWhitespaceIndex(lineText);
            if (firstNonWhitespaceIndex < 0)
            {
                return false;
            }

            return firstNonWhitespaceIndex + 1 < lineText.Length &&
                   lineText[firstNonWhitespaceIndex] == '/' &&
                   lineText[firstNonWhitespaceIndex + 1] == '/';
        }

        /// <summary>
        /// 获取一行中首个非空白字符的位置。
        /// 若整行为空白，则返回 -1。
        /// </summary>
        private static int GetFirstNonWhitespaceIndex(string lineText)
        {
            for (int i = 0; i < lineText.Length; i++)
            {
                if (!char.IsWhiteSpace(lineText[i]))
                {
                    return i;
                }
            }

            return -1;
        }
    }

    [Export(typeof(IVsTextViewCreationListener))]
    [ContentType("darkest-effect")]
    [ContentType("darkest-info")]
    [ContentType("darkest-art")]
    [ContentType("darkest-override")]
    [ContentType("darkest-colours")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class DarkestToggleCommentTextViewCreationListener : IVsTextViewCreationListener
    {
        [Import]
        internal IVsEditorAdaptersFactoryService AdapterService = null;

        [Import]
        internal ITextUndoHistoryRegistry UndoHistoryRegistry = null;

        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            IWpfTextView textView = AdapterService.GetWpfTextView(textViewAdapter);
            if (textView == null)
            {
                return;
            }

            // 使用同一个实例同时处理按键与命令链，避免状态分裂。
            var filter = new DarkestToggleCommentCommandFilter(textView, UndoHistoryRegistry, For_the_Darkest_DungeonPackage.Instance);
            filter.AttachKeyHandler();
            textViewAdapter.AddCommandFilter(filter, out IOleCommandTarget next);
            filter.Next = next;
        }
    }
}