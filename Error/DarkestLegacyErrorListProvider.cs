using For_the_Darkest_Dungeon.Classification;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace For_the_Darkest_Dungeon.Error
{
	[Export(typeof(IWpfTextViewCreationListener))]
	[ContentType("darkest-effect")]
	[ContentType("darkest-info")]
	[ContentType("darkest-art")]
	[ContentType("darkest-override")]
	[TextViewRole(PredefinedTextViewRoles.Document)]
	internal sealed class DarkestLegacyErrorListManager : IWpfTextViewCreationListener
	{
		private static ErrorListProvider _errorListProvider;

		public void TextViewCreated(IWpfTextView textView)
		{
			if (textView == null)
				return;

			try
			{
				if (_errorListProvider == null)
				{
					_errorListProvider = new ErrorListProvider(ServiceProvider.GlobalProvider)
					{
						ProviderName = "Darkest Dungeon"
					};
				}

				ITextBuffer buffer = textView.TextBuffer;
				if (buffer == null)
					return;

				if (buffer.Properties.ContainsProperty(typeof(ErrorTaskSink)))
					return;

				var sink = new ErrorTaskSink(buffer, _errorListProvider);
				buffer.Properties.AddProperty(typeof(ErrorTaskSink), sink);

				textView.Closed += (s, e) =>
				{
					try
					{
						if (buffer.Properties.TryGetProperty(typeof(ErrorTaskSink), out ErrorTaskSink existing))
						{
							existing.Dispose();
							buffer.Properties.RemoveProperty(typeof(ErrorTaskSink));
						}
					}
					catch (Exception ex)
					{
						System.Diagnostics.Debug.WriteLine("DarkestLegacyErrorListManager cleanup failed: " + ex);
					}
				};
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine("DarkestLegacyErrorListManager initialization failed: " + ex);
			}
		}
	}

	internal sealed class ErrorTaskSink : IDisposable
	{
		private readonly ITextBuffer _buffer;
		private readonly ErrorListProvider _errorListProvider;
		private readonly List<ErrorTask> _ownedTasks = new List<ErrorTask>();

		private ITagger<IErrorTag> _tagger;
		private CancellationTokenSource _refreshCts;
		private bool _disposed;

		public ErrorTaskSink(ITextBuffer buffer, ErrorListProvider errorListProvider)
		{
			_buffer = buffer;
			_errorListProvider = errorListProvider;

			_tagger = GetOrCreateErrorTagger(buffer);

			if (_tagger != null)
				_tagger.TagsChanged += OnTagsChanged;

			// _buffer.Changed += OnBufferChanged;

			ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
			{
				await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
				RefreshErrors();
			}).FileAndForget("For_the_Darkest_Dungeon/ErrorTaskSink/InitialRefresh");
		}

		private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
		{
			ScheduleRefresh();
		}

		private void OnTagsChanged(object sender, SnapshotSpanEventArgs e)
		{
			ScheduleRefresh();
		}

		private void ScheduleRefresh()
		{
			if (_disposed)
				return;

			_refreshCts?.Cancel();
			_refreshCts?.Dispose();

			_refreshCts = new CancellationTokenSource();
			CancellationToken token = _refreshCts.Token;

			ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
			{
				try
				{
					await Task.Delay(300, token);

					if (token.IsCancellationRequested || _disposed)
						return;

					await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);

					if (token.IsCancellationRequested || _disposed)
						return;

					RefreshErrors();
				}
				catch (OperationCanceledException)
				{
					// 防抖取消，正常情况。
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine("ErrorTaskSink delayed refresh failed: " + ex);
				}
			}).FileAndForget("For_the_Darkest_Dungeon/ErrorTaskSink/DelayedRefresh");
		}

		private void RefreshErrors()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			if (_disposed)
				return;

			ClearOwnedTasks();

			if (_tagger == null)
				return;

			string filePath = TryGetFilePath(_buffer);
			if (string.IsNullOrEmpty(filePath))
				return;

			ITextSnapshot snapshot = _buffer.CurrentSnapshot;
			if (snapshot == null)
				return;

			var fullSpan = new SnapshotSpan(snapshot, 0, snapshot.Length);
			var spans = new NormalizedSnapshotSpanCollection(fullSpan);

			List<ITagSpan<IErrorTag>> tags;

			try
			{
				tags = _tagger.GetTags(spans).ToList();
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine("ErrorTaskSink GetTags failed: " + ex);
				return;
			}

			foreach (ITagSpan<IErrorTag> tagSpan in tags)
			{
				if (tagSpan == null || tagSpan.Tag == null)
					continue;

				SnapshotSpan span = tagSpan.Span;

				if (span.Snapshot != snapshot)
				{
					try
					{
						span = span.TranslateTo(snapshot, SpanTrackingMode.EdgeInclusive);
					}
					catch
					{
						continue;
					}
				}

				ITextSnapshotLine line = span.Start.GetContainingLine();
				int lineNumber = line.LineNumber;
				int column = span.Start.Position - line.Start.Position;

				string message = tagSpan.Tag.ToolTipContent as string;
				if (string.IsNullOrWhiteSpace(message))
					message = tagSpan.Tag.ErrorType ?? "Darkest Dungeon error";

				TaskErrorCategory category = ConvertErrorCategory(tagSpan.Tag.ErrorType);

				var task = new ErrorTask
				{
					ErrorCategory = category,
					Category = TaskCategory.CodeSense,
					Document = filePath,
					Line = lineNumber,
					Column = column,
					Text = message
				};

				task.Navigate += OnTaskNavigate;

				_errorListProvider.Tasks.Add(task);
				_ownedTasks.Add(task);
			}

			// _errorListProvider.Show();
		}

		private void ClearOwnedTasks()
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			foreach (ErrorTask task in _ownedTasks)
			{
				task.Navigate -= OnTaskNavigate;
				_errorListProvider.Tasks.Remove(task);
			}

			_ownedTasks.Clear();
		}

		private void OnTaskNavigate(object sender, EventArgs e)
		{
			ThreadHelper.ThrowIfNotOnUIThread();

			if (!(sender is ErrorTask task))
				return;

			if (string.IsNullOrEmpty(task.Document))
				return;

			try
			{
				IVsUIHierarchy hierarchy;
				uint itemId;
				IVsWindowFrame windowFrame;
				IVsTextView textView;

				VsShellUtilities.OpenDocument(
					ServiceProvider.GlobalProvider,
					task.Document,
					Guid.Empty,
					out hierarchy,
					out itemId,
					out windowFrame,
					out textView);

				if (textView != null)
				{
					textView.SetCaretPos(task.Line, task.Column);
					textView.CenterLines(task.Line, 1);
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine("ErrorTask navigation failed: " + ex);
			}
		}

		private static TaskErrorCategory ConvertErrorCategory(string errorType)
		{
			if (errorType == PredefinedErrorTypeNames.Warning)
				return TaskErrorCategory.Warning;

			if (errorType == PredefinedErrorTypeNames.Suggestion)
				return TaskErrorCategory.Message;

			return TaskErrorCategory.Error;
		}

		private static ITagger<IErrorTag> GetOrCreateErrorTagger(ITextBuffer buffer)
		{
			if (buffer == null || buffer.ContentType == null)
				return null;

			string contentTypeName = buffer.ContentType.TypeName;

			switch (contentTypeName)
			{
				case "darkest-effect":
					return buffer.Properties.GetOrCreateSingletonProperty(
						() => new EffectErrorTagger(buffer));

				case "darkest-info":
					return buffer.Properties.GetOrCreateSingletonProperty(
						() => new InfoErrorTagger(buffer));

				case "darkest-art":
					return buffer.Properties.GetOrCreateSingletonProperty(
						() => new ArtErrorTagger(buffer));

				case "darkest-override":
					return buffer.Properties.GetOrCreateSingletonProperty(
						() => new OverrideErrorTagger(buffer));

				default:
					return null;
			}
		}

		private static string TryGetFilePath(ITextBuffer buffer)
		{
			if (buffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument doc))
				return doc.FilePath;

			return null;
		}

		public void Dispose()
		{
			if (_disposed)
				return;

			_disposed = true;

			// _buffer.Changed -= OnBufferChanged;

			if (_tagger != null)
				_tagger.TagsChanged -= OnTagsChanged;

			_refreshCts?.Cancel();
			_refreshCts?.Dispose();
			_refreshCts = null;

			ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
			{
				await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

				try
				{
					ClearOwnedTasks();
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine("ErrorTaskSink dispose cleanup failed: " + ex);
				}
			}).FileAndForget("For_the_Darkest_Dungeon/ErrorTaskSink/Dispose");
		}
	}
}