using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace For_the_Darkest_Dungeon.Classification
{
	/// <summary>
	/// 为 Colours 文件中的 .rgba 参数渲染颜色预览方块。
	/// </summary>
	internal sealed class ColoursColorAdornmentTagger : ITagger<IntraTextAdornmentTag>
	{
		private readonly IWpfTextView _textView;
		private readonly ITextBuffer _buffer;
		private static readonly Regex RgbaKeywordRegex = new Regex(@"\.rgba\s+(?<args>[^\r\n/]*)", RegexOptions.Compiled);
		private static readonly Regex HexColorRegex = new Regex(@"^#(?<hex>[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$", RegexOptions.Compiled);
		private static readonly Regex NumericColorRegex = new Regex(@"^(?<r>\d{1,3})\s+(?<g>\d{1,3})\s+(?<b>\d{1,3})\s+(?<a>\d{1,3})$", RegexOptions.Compiled);

		internal ColoursColorAdornmentTagger(IWpfTextView textView, ITextBuffer buffer)
		{
			_textView = textView;
			_buffer = buffer;
			_buffer.Changed += OnBufferChanged;
		}

		public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

		public IEnumerable<ITagSpan<IntraTextAdornmentTag>> GetTags(NormalizedSnapshotSpanCollection spans)
		{
			if (spans == null || spans.Count == 0)
				yield break;

			if (!ShouldRenderColorPreview())
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

				Group argsGroup = match.Groups["args"];
				if (argsGroup.Length <= 0)
					continue;

				string args = argsGroup.Value.Trim();
				Color? color = TryParseColor(args);
				if (!color.HasValue)
					continue;

				int argsStart = line.Start.Position + argsGroup.Index;
				int argsLength = argsGroup.Length;
				int adornmentPosition = argsStart + argsLength;
				SnapshotSpan argsSpan = new SnapshotSpan(snapshot, argsStart, argsLength);
				var adornmentSpan = new SnapshotSpan(snapshot, adornmentPosition, 0);
				var tag = new IntraTextAdornmentTag(CreateAdornment(argsSpan, color.Value), null);
				yield return new TagSpan<IntraTextAdornmentTag>(adornmentSpan, tag);
			}
		}

		private bool ShouldRenderColorPreview()
		{
			return GetOptions().EnableAutomaticColorPreview;
		}

		private bool ShouldOpenColorPickerOnClick()
		{
			return GetOptions().EnableColorPickerOnClick;
		}

		private ColoursOptionsPage GetOptions()
		{
			For_the_Darkest_DungeonPackage package = Package.GetGlobalService(typeof(For_the_Darkest_DungeonPackage)) as For_the_Darkest_DungeonPackage;
			if (package == null)
				return new ColoursOptionsPage();

			return package.GetDialogPage(typeof(ColoursOptionsPage)) as ColoursOptionsPage ?? new ColoursOptionsPage();
		}

		private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
		{
			var handler = TagsChanged;
			if (handler == null)
				return;

			ITextSnapshot snapshot = e.After;
			handler(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length)));
		}

		private UIElement CreateAdornment(SnapshotSpan argsSpan, Color color)
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
				Cursor = Cursors.Hand,
				ToolTip = string.Format(CultureInfo.InvariantCulture, "点击选择颜色并覆写为 #RRGGBB，当前颜色：RGBA({0}, {1}, {2}, {3})", color.R, color.G, color.B, color.A)
			};

			border.SnapsToDevicePixels = true;
			RenderOptions.SetEdgeMode(border, EdgeMode.Aliased);
			border.MouseLeftButtonUp += (sender, e) =>
			{
				e.Handled = true;
				if (!ShouldOpenColorPickerOnClick())
					return;

				OpenColorPickerAndReplace(argsSpan, color);
			};

			return border;
		}

		private void OpenColorPickerAndReplace(SnapshotSpan argsSpan, Color initialColor)
		{
			ColorPickerWindow window = new ColorPickerWindow(initialColor);
			Window owner = Window.GetWindow(_textView.VisualElement);
			if (owner != null)
				window.Owner = owner;

			bool? result = window.ShowDialog();
			if (result != true || !window.SelectedColor.HasValue)
				return;

			Color selectedColor = window.SelectedColor.Value;
			string replacement = string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", selectedColor.R, selectedColor.G, selectedColor.B);
			ReplaceColorText(argsSpan, replacement);
		}

		private void ReplaceColorText(SnapshotSpan argsSpan, string replacement)
		{
			ITextSnapshot currentSnapshot = _buffer.CurrentSnapshot;
			if (argsSpan.Start.Position > currentSnapshot.Length)
				return;

			SnapshotSpan translatedSpan = argsSpan.TranslateTo(currentSnapshot, SpanTrackingMode.EdgeInclusive);
			using (ITextEdit edit = _buffer.CreateEdit())
			{
				edit.Replace(translatedSpan.Span, replacement);
				edit.Apply();
			}
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

	internal sealed class ColorPickerWindow : Window
	{
		private readonly Rectangle _paletteRectangle;
		private readonly Border _previewBorder;
		private readonly TextBlock _hexText;
		private readonly Slider _redSlider;
		private readonly Slider _greenSlider;
		private readonly Slider _blueSlider;
		private bool _isUpdatingFromPalette;

		internal ColorPickerWindow(Color initialColor)
		{
			Title = "颜色选择";
			Width = 320;
			Height = 400;
			MinWidth = 320;
			MinHeight = 320;
			WindowStartupLocation = WindowStartupLocation.CenterOwner;
			ResizeMode = ResizeMode.NoResize;
			ShowInTaskbar = false;
			Background = Brushes.White;

			var scrollViewer = new ScrollViewer
			{
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
				HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
				CanContentScroll = true
			};

			var root = new Grid
			{
				Margin = new Thickness(12, 12, 12, 12)
			};
			root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

			_previewBorder = new Border
			{
				Height = 28,
				CornerRadius = new CornerRadius(3),
				BorderThickness = new Thickness(1),
				BorderBrush = Brushes.Gray,
				Margin = new Thickness(0, 0, 0, 6)
			};
			Grid.SetRow(_previewBorder, 0);
			root.Children.Add(_previewBorder);

			_paletteRectangle = new Rectangle
			{
				Width = 240,
				Height = 180,
				Stroke = Brushes.Gray,
				StrokeThickness = 1,
				Margin = new Thickness(0, 0, 0, 6),
				HorizontalAlignment = HorizontalAlignment.Left,
				Fill = CreatePaletteBrush()
			};
			_paletteRectangle.MouseLeftButtonDown += PaletteRectangle_MouseLeftButtonDown;
			_paletteRectangle.MouseMove += PaletteRectangle_MouseMove;
			_paletteRectangle.MouseLeftButtonUp += (sender, e) => _paletteRectangle.ReleaseMouseCapture();
			Grid.SetRow(_paletteRectangle, 1);
			root.Children.Add(_paletteRectangle);

			_hexText = new TextBlock
			{
				Margin = new Thickness(0, 0, 0, 6)
			};
			Grid.SetRow(_hexText, 2);
			root.Children.Add(_hexText);

			_redSlider = CreateSliderRow(root, 3, "R", initialColor.R);
			_greenSlider = CreateSliderRow(root, 4, "G", initialColor.G);
			_blueSlider = CreateSliderRow(root, 5, "B", initialColor.B);

			var buttonPanel = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				HorizontalAlignment = HorizontalAlignment.Right,
				Margin = new Thickness(0, 12, 0, 0)
			};

			var okButton = new Button
			{
				Content = "确定",
				MinWidth = 62,
				Padding = new Thickness(8, 3, 8, 3),
				Margin = new Thickness(0, 0, 8, 0),
				IsDefault = true
			};
			okButton.Click += (sender, e) =>
			{
				SelectedColor = BuildCurrentColor();
				DialogResult = true;
				Close();
			};

			var cancelButton = new Button
			{
				Content = "取消",
				MinWidth = 62,
				Padding = new Thickness(8, 3, 8, 3),
				IsCancel = true
			};
			cancelButton.Click += (sender, e) =>
			{
				DialogResult = false;
				Close();
			};

			buttonPanel.Children.Add(okButton);
			buttonPanel.Children.Add(cancelButton);
			Grid.SetRow(buttonPanel, 6);
			root.Children.Add(buttonPanel);

			scrollViewer.Content = root;
			Content = scrollViewer;

			AttachSliderPreview(_redSlider);
			AttachSliderPreview(_greenSlider);
			AttachSliderPreview(_blueSlider);
			UpdatePreview();
		}

		internal Color? SelectedColor { get; private set; }

		private static Brush CreatePaletteBrush()
		{
			var horizontal = new LinearGradientBrush
			{
				StartPoint = new Point(0, 0),
				EndPoint = new Point(1, 0)
			};
			horizontal.GradientStops.Add(new GradientStop(Colors.White, 0));
			horizontal.GradientStops.Add(new GradientStop(Colors.Red, 0.17));
			horizontal.GradientStops.Add(new GradientStop(Colors.Yellow, 0.34));
			horizontal.GradientStops.Add(new GradientStop(Colors.Lime, 0.51));
			horizontal.GradientStops.Add(new GradientStop(Colors.Cyan, 0.68));
			horizontal.GradientStops.Add(new GradientStop(Colors.Blue, 0.85));
			horizontal.GradientStops.Add(new GradientStop(Colors.Magenta, 1));

			var vertical = new LinearGradientBrush
			{
				StartPoint = new Point(0, 0),
				EndPoint = new Point(0, 1)
			};
			vertical.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0));
			vertical.GradientStops.Add(new GradientStop(Color.FromArgb(255, 0, 0, 0), 1));

			var drawingGroup = new DrawingGroup();
			drawingGroup.Children.Add(new GeometryDrawing(horizontal, null, new RectangleGeometry(new Rect(0, 0, 1, 1))));
			drawingGroup.Children.Add(new GeometryDrawing(vertical, null, new RectangleGeometry(new Rect(0, 0, 1, 1))));
			return new DrawingBrush(drawingGroup)
			{
				Stretch = Stretch.Fill
			};
		}

		private Slider CreateSliderRow(Grid parent, int rowIndex, string label, byte initialValue)
		{
			var row = new Grid
			{
				Margin = new Thickness(0, 0, 0, 5),
				Width = 260,
				HorizontalAlignment = HorizontalAlignment.Left
			};
			row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
			row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

			var text = new TextBlock
			{
				Text = label,
				Width = 18,
				Margin = new Thickness(0, 0, 6, 0),
				VerticalAlignment = VerticalAlignment.Center
			};
			Grid.SetColumn(text, 0);
			row.Children.Add(text);

			var slider = new Slider
			{
				Minimum = 0,
				Maximum = 255,
				Value = initialValue,
				TickFrequency = 1,
				IsSnapToTickEnabled = true,
				Width = 200,
				VerticalAlignment = VerticalAlignment.Center
			};
			Grid.SetColumn(slider, 1);
			row.Children.Add(slider);

			var valueText = new TextBlock
			{
				Width = 30,
				Margin = new Thickness(6, 0, 0, 0),
				VerticalAlignment = VerticalAlignment.Center,
				Text = initialValue.ToString(CultureInfo.InvariantCulture)
			};
			slider.ValueChanged += (sender, e) =>
			{
				valueText.Text = ((int)slider.Value).ToString(CultureInfo.InvariantCulture);
			};
			Grid.SetColumn(valueText, 2);
			row.Children.Add(valueText);

			Grid.SetRow(row, rowIndex);
			parent.Children.Add(row);
			return slider;
		}

		private void AttachSliderPreview(Slider slider)
		{
			slider.ValueChanged += (sender, e) =>
			{
				if (_isUpdatingFromPalette)
					return;

				UpdatePreview();
			};
		}

		private void PaletteRectangle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			UpdateColorFromPalette(e.GetPosition(_paletteRectangle));
			_paletteRectangle.CaptureMouse();
		}

		private void PaletteRectangle_MouseMove(object sender, MouseEventArgs e)
		{
			if (!_paletteRectangle.IsMouseCaptured || e.LeftButton != MouseButtonState.Pressed)
				return;

			UpdateColorFromPalette(e.GetPosition(_paletteRectangle));
		}

		private void UpdateColorFromPalette(Point position)
		{
			double width = _paletteRectangle.ActualWidth;
			double height = _paletteRectangle.ActualHeight;
			if (width <= 0 || height <= 0)
				return;

			double x = Clamp(position.X, 0, width);
			double y = Clamp(position.Y, 0, height);

			double hue = x / width;
			double brightness = 1.0 - (y / height);
			Color paletteColor = FromHueAndBrightness(hue, brightness);

			_isUpdatingFromPalette = true;
			_redSlider.Value = paletteColor.R;
			_greenSlider.Value = paletteColor.G;
			_blueSlider.Value = paletteColor.B;
			_isUpdatingFromPalette = false;
			UpdatePreview();
		}

		private void UpdatePreview()
		{
			Color color = BuildCurrentColor();
			_previewBorder.Background = new SolidColorBrush(color);
			_hexText.Text = string.Format(CultureInfo.InvariantCulture, "将覆写为：#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);
		}

		private Color BuildCurrentColor()
		{
			return Color.FromRgb((byte)_redSlider.Value, (byte)_greenSlider.Value, (byte)_blueSlider.Value);
		}

		private static double Clamp(double value, double min, double max)
		{
			if (value < min)
				return min;
			if (value > max)
				return max;
			return value;
		}

		private static Color FromHueAndBrightness(double hue, double brightness)
		{
			hue = Clamp(hue, 0, 1);
			brightness = Clamp(brightness, 0, 1);

			double scaledHue = hue * 6;
			int region = (int)Math.Floor(scaledHue);
			double fraction = scaledHue - region;
			byte value = (byte)Math.Round(brightness * 255);
			byte rising = (byte)Math.Round(brightness * fraction * 255);
			byte falling = (byte)Math.Round(brightness * (1 - fraction) * 255);

			switch (region % 6)
			{
				case 0:
					return Color.FromRgb(value, rising, 0);
				case 1:
					return Color.FromRgb(falling, value, 0);
				case 2:
					return Color.FromRgb(0, value, rising);
				case 3:
					return Color.FromRgb(0, falling, value);
				case 4:
					return Color.FromRgb(rising, 0, value);
				default:
					return Color.FromRgb(value, 0, falling);
			}
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
				() => new ColoursColorAdornmentTagger(textView as IWpfTextView, buffer)) as ITagger<T>;
		}
	}
}