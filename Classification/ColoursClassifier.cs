using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text.RegularExpressions;

namespace For_the_Darkest_Dungeon.Classification

{

	/// <summary>
	/// Colours 文件专用着色器。
	/// 该文件类型不继承 InfoBaseClassifier，而是独立维护自己的着色规则。
	/// </summary>

	internal sealed class ColoursClassifier : IClassifier
	{
		private readonly IClassificationTypeRegistryService _registry;

		private static readonly Regex HeaderRegex = new Regex(@"^[ \t]*(?<header>colour:)", RegexOptions.Compiled);
		private static readonly Regex KeywordRegex = new Regex(@"\.[a-zA-Z_][a-zA-Z0-9_]*", RegexOptions.Compiled);
		private static readonly Regex NumberRegex = new Regex(@"-?\d+(\.\d+)?%?", RegexOptions.Compiled);
		private static readonly Regex StringRegex = new Regex(@"""[^""]*""", RegexOptions.Compiled);
		private static readonly Regex UnquotedRegex = new Regex(@"\b[a-zA-Z_][a-zA-Z0-9_#]*\b|#[0-9a-fA-F]+", RegexOptions.Compiled);

		internal ColoursClassifier(IClassificationTypeRegistryService registry)
		{
			_registry = registry;
		}

		public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span)
		{
			var list = new List<ClassificationSpan>();
			string text = span.GetText();

			int commentIndex = text.IndexOf("//", StringComparison.Ordinal);
			if (commentIndex >= 0)
			{
				var commentType = _registry.GetClassificationType("darkest.comment");
				list.Add(new ClassificationSpan(
					new SnapshotSpan(span.Snapshot, span.Start + commentIndex, text.Length - commentIndex),
					commentType));
			}

			int codeLength = commentIndex >= 0 ? commentIndex : text.Length;
			string codeText = text.Substring(0, codeLength);

			if (codeLength == 0)
				return list;

			foreach (Match match in HeaderRegex.Matches(codeText))
			{
				var type = _registry.GetClassificationType("darkest.header");
				var headerGroup = match.Groups["header"];

				list.Add(new ClassificationSpan(
					new SnapshotSpan(span.Snapshot, span.Start + headerGroup.Index, headerGroup.Length),
					type));
			}

			foreach (Match match in KeywordRegex.Matches(codeText))
			{
				if (match.Index > 0 && char.IsDigit(codeText[match.Index - 1]))
					continue;

				var type = _registry.GetClassificationType("darkest.info.keyword");

				list.Add(new ClassificationSpan(
					new SnapshotSpan(span.Snapshot, span.Start + match.Index, match.Length),
					type));
			}

			foreach (Match match in StringRegex.Matches(codeText))
			{
				var type = _registry.GetClassificationType("darkest.string");

				list.Add(new ClassificationSpan(
					new SnapshotSpan(span.Snapshot, span.Start + match.Index, match.Length),
					type));
			}

			foreach (Match match in UnquotedRegex.Matches(codeText))
			{
				var currentSpan = new Span((span.Start + match.Index).Position, match.Length);

				if (list.Any(s => s.Span.IntersectsWith(currentSpan)))
					continue;

				var type = _registry.GetClassificationType("darkest.unquoted");

				if (string.Equals(match.Value, "true", StringComparison.Ordinal) ||
					string.Equals(match.Value, "false", StringComparison.Ordinal) ||
					string.Equals(match.Value, "True", StringComparison.Ordinal) ||
					string.Equals(match.Value, "False", StringComparison.Ordinal) ||
					string.Equals(match.Value, "TRUE", StringComparison.Ordinal) ||
					string.Equals(match.Value, "FALSE", StringComparison.Ordinal))
				{
					type = _registry.GetClassificationType("darkest.bool");
				}

				list.Add(new ClassificationSpan(
					new SnapshotSpan(span.Snapshot, span.Start + match.Index, match.Length),
					type));
			}

			foreach (Match match in NumberRegex.Matches(codeText))
			{
				var currentSpan = new Span((span.Start + match.Index).Position, match.Length);
				if (list.Any(s => s.Span.IntersectsWith(currentSpan)))
					continue;

				var type = _registry.GetClassificationType("darkest.number");

				list.Add(new ClassificationSpan(
					new SnapshotSpan(span.Snapshot, span.Start + match.Index, match.Length),
					type));
			}

			return list;
		}

		public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;

	}

	[Export(typeof(IClassifierProvider))]
	[ContentType("darkest-colours")]
	internal class ColoursClassifierProvider : IClassifierProvider
	{
		[Import]
		internal IClassificationTypeRegistryService classificationRegistry;

		public IClassifier GetClassifier(ITextBuffer buffer)
		{
			return buffer.Properties.GetOrCreateSingletonProperty(
				() => new ColoursClassifier(classificationRegistry));
		}
	}
}
