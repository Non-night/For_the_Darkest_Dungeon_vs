using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Text.RegularExpressions;



namespace For_the_Darkest_Dungeon.Classification

{

	/// <summary>
	/// 为 Colours 文件中的 .rgba 参数渲染颜色预览方块。
	/// </summary>

	internal sealed class ColoursColorAdornmentTagger : ITagger<IntraTextAdornmentTag>

	{
		private readonly ITextBuffer _buffer;
		private static readonly Regex RgbaKeywordRegex = new Regex(@"\.rgba\s+(?<args>[^\r\n/]*)", RegexOptions.Compiled);
		private static readonly Regex HexColorRegex = new Regex(@"^#(?<hex>[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$", RegexOptions.Compiled);
		private static readonly Regex NumericColorRegex = new Regex(@"^(?<r>\d{1,3})\s+(?<g>\d{1,3})\s+(?<b>\d{1,3})\s+(?<a>\d{1,3})$", RegexOptions.Compiled);

		internal ColoursColorAdornmentTagger(ITextBuffer buffer)
		{
			_buffer = buffer;
			_buffer.Changed += OnBufferChanged;
		}

		public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

		public IEnumerable<ITagSpan<IntraTextAdornmentTag>> GetTags(NormalizedSnapshotSpanCollection spans)
		{
			if (spans == null || spans.Count == 0)
				yield break;

			ITextSnapshot snapshot = spans[0].Snapshot;
			foreach (ITextSnapshotLine line in snapshot.Lines)
			{
				string lineText = line.GetText();
				int commentIndex = lineText.IndexOf("//", StringComparison.Ordinal);

				string codeText = commentIndex >= 0 ? lineText.Substring(0, commentIndex) : lineText;

				Match match = RgbaKeywordRegex.Match(codeText);

				if (!match.Success)

					continue;



				string args = match.Groups["args"].Value.Trim();

				Color? color = TryParseColor(args);

				if (!color.HasValue)

					continue;



				Group argsGroup = match.Groups["args"];

				if (argsGroup.Length <= 0)

					continue;



				int adornmentPosition = line.Start.Position + argsGroup.Index + argsGroup.Length;

				var adornmentSpan = new SnapshotSpan(snapshot, adornmentPosition, 0);

				var tag = new IntraTextAdornmentTag(CreateAdornment(color.Value), null);

				yield return new TagSpan<IntraTextAdornmentTag>(adornmentSpan, tag);

			}

		}



		private void OnBufferChanged(object sender, TextContentChangedEventArgs e)

		{

			var handler = TagsChanged;

			if (handler == null)

				return;



			ITextSnapshot snapshot = e.After;

			handler(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length)));

		}



		private static UIElement CreateAdornment(Color color)

		{

			var border = new Border

			{

				Width = 12,

				Height = 12,

				Margin = new Thickness(4, 0, 0, 0),

				CornerRadius = new CornerRadius(1),

				BorderThickness = new Thickness(1),

				BorderBrush = Brushes.Gray,

				Background = new SolidColorBrush(color),

				ToolTip = string.Format(CultureInfo.InvariantCulture, "RGBA({0}, {1}, {2}, {3})", color.R, color.G, color.B, color.A)

			};



			border.SnapsToDevicePixels = true;

			RenderOptions.SetEdgeMode(border, EdgeMode.Aliased);

			return border;

		}



		private static Color? TryParseColor(string value)

		{

			if (string.IsNullOrWhiteSpace(value))

				return null;



			Match hexMatch = HexColorRegex.Match(value);

			if (hexMatch.Success)

			{

				string hex = hexMatch.Groups["hex"].Value;

				if (hex.Length == 3)

				{

					byte rShort = ParseHexByte(new string(hex[0], 2));

					byte gShort = ParseHexByte(new string(hex[1], 2));

					byte bShort = ParseHexByte(new string(hex[2], 2));

					return Color.FromArgb(255, rShort, gShort, bShort);

				}



				byte r = ParseHexByte(hex.Substring(0, 2));

				byte g = ParseHexByte(hex.Substring(2, 2));

				byte b = ParseHexByte(hex.Substring(4, 2));

				return Color.FromArgb(255, r, g, b);

			}



			Match numericMatch = NumericColorRegex.Match(value);

			if (numericMatch.Success)

			{

				int r;

				int g;

				int b;

				int a;

				if (!int.TryParse(numericMatch.Groups["r"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out r) ||

					!int.TryParse(numericMatch.Groups["g"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out g) ||

					!int.TryParse(numericMatch.Groups["b"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out b) ||

					!int.TryParse(numericMatch.Groups["a"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out a))

				{

					return null;

				}



				if (!IsByteRange(r) || !IsByteRange(g) || !IsByteRange(b) || !IsByteRange(a))

					return null;



				return Color.FromArgb((byte)a, (byte)r, (byte)g, (byte)b);

			}



			return null;

		}



		private static bool IsByteRange(int value)

		{

			return value >= 0 && value <= 255;

		}



		private static byte ParseHexByte(string hex)

		{

			return byte.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

		}

	}



	[Export(typeof(IViewTaggerProvider))]

	[ContentType("darkest-colours")]

	[TagType(typeof(IntraTextAdornmentTag))]

	internal sealed class ColoursColorAdornmentTaggerProvider : IViewTaggerProvider

	{

		public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag

		{

			if (textView.TextBuffer != buffer)

				return null;



			return buffer.Properties.GetOrCreateSingletonProperty(

				() => new ColoursColorAdornmentTagger(buffer)) as ITagger<T>;

		}

	}

}

