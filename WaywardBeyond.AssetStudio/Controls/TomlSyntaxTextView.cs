using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WaywardBeyond.AssetStudio.Controls;

/// <summary>
///     Renders line-numbered syntax-highlighted TOML source text with optional error line emphasis.
/// </summary>
public sealed class TomlSyntaxTextView : Control
{
    /// <summary>
    ///     Styled property supplying the TOML document text to render.
    /// </summary>
    public static readonly StyledProperty<string?> SourceTextProperty =
        AvaloniaProperty.Register<TomlSyntaxTextView, string?>(nameof(SourceText));

    /// <summary>
    ///     Styled property highlighting an optional 1-based error line number.
    /// </summary>
    public static readonly StyledProperty<int> ErrorLineNumberProperty =
        AvaloniaProperty.Register<TomlSyntaxTextView, int>(nameof(ErrorLineNumber));

    /// <summary>
    ///     Styled property configuring default foreground brush.
    /// </summary>
    public static readonly StyledProperty<IBrush?> TextBrushProperty =
        AvaloniaProperty.Register<TomlSyntaxTextView, IBrush?>(nameof(TextBrush));

    /// <summary>
    ///     Styled property configuring comment token brush.
    /// </summary>
    public static readonly StyledProperty<IBrush?> CommentBrushProperty =
        AvaloniaProperty.Register<TomlSyntaxTextView, IBrush?>(nameof(CommentBrush));

    /// <summary>
    ///     Styled property configuring section header token brush.
    /// </summary>
    public static readonly StyledProperty<IBrush?> SectionBrushProperty =
        AvaloniaProperty.Register<TomlSyntaxTextView, IBrush?>(nameof(SectionBrush));

    /// <summary>
    ///     Styled property configuring key identifier token brush.
    /// </summary>
    public static readonly StyledProperty<IBrush?> KeyBrushProperty =
        AvaloniaProperty.Register<TomlSyntaxTextView, IBrush?>(nameof(KeyBrush));

    /// <summary>
    ///     Styled property configuring string literal token brush.
    /// </summary>
    public static readonly StyledProperty<IBrush?> StringBrushProperty =
        AvaloniaProperty.Register<TomlSyntaxTextView, IBrush?>(nameof(StringBrush));

    /// <summary>
    ///     Styled property configuring numeric and boolean literal brush.
    /// </summary>
    public static readonly StyledProperty<IBrush?> NumberBrushProperty =
        AvaloniaProperty.Register<TomlSyntaxTextView, IBrush?>(nameof(NumberBrush));

    /// <summary>
    ///     Styled property configuring line number gutter foreground brush.
    /// </summary>
    public static readonly StyledProperty<IBrush?> GutterBrushProperty =
        AvaloniaProperty.Register<TomlSyntaxTextView, IBrush?>(nameof(GutterBrush));

    /// <summary>
    ///     Styled property configuring error highlight background brush.
    /// </summary>
    public static readonly StyledProperty<IBrush?> ErrorBackgroundBrushProperty =
        AvaloniaProperty.Register<TomlSyntaxTextView, IBrush?>(nameof(ErrorBackgroundBrush));

    private const double LINE_HEIGHT = 20.0;
    private const double GUTTER_WIDTH = 42.0;

    static TomlSyntaxTextView()
    {
        AffectsMeasure<TomlSyntaxTextView>(SourceTextProperty);
        AffectsRender<TomlSyntaxTextView>(
            SourceTextProperty,
            ErrorLineNumberProperty,
            TextBrushProperty,
            CommentBrushProperty,
            SectionBrushProperty,
            KeyBrushProperty,
            StringBrushProperty,
            NumberBrushProperty,
            GutterBrushProperty,
            ErrorBackgroundBrushProperty);
    }

    /// <summary>
    ///     TOML source text rendered by this control.
    /// </summary>
    public string? SourceText
    {
        get => GetValue(SourceTextProperty);
        set => SetValue(SourceTextProperty, value);
    }

    /// <summary>
    ///     Optional 1-based line index highlighted as a syntax or reference error.
    /// </summary>
    public int ErrorLineNumber
    {
        get => GetValue(ErrorLineNumberProperty);
        set => SetValue(ErrorLineNumberProperty, value);
    }

    /// <summary>
    ///     Default text foreground brush.
    /// </summary>
    public IBrush? TextBrush
    {
        get => GetValue(TextBrushProperty);
        set => SetValue(TextBrushProperty, value);
    }

    /// <summary>
    ///     Foreground brush for TOML comment lines.
    /// </summary>
    public IBrush? CommentBrush
    {
        get => GetValue(CommentBrushProperty);
        set => SetValue(CommentBrushProperty, value);
    }

    /// <summary>
    ///     Foreground brush for TOML table headers.
    /// </summary>
    public IBrush? SectionBrush
    {
        get => GetValue(SectionBrushProperty);
        set => SetValue(SectionBrushProperty, value);
    }

    /// <summary>
    ///     Foreground brush for TOML assignment keys.
    /// </summary>
    public IBrush? KeyBrush
    {
        get => GetValue(KeyBrushProperty);
        set => SetValue(KeyBrushProperty, value);
    }

    /// <summary>
    ///     Foreground brush for TOML string values.
    /// </summary>
    public IBrush? StringBrush
    {
        get => GetValue(StringBrushProperty);
        set => SetValue(StringBrushProperty, value);
    }

    /// <summary>
    ///     Foreground brush for numbers and booleans.
    /// </summary>
    public IBrush? NumberBrush
    {
        get => GetValue(NumberBrushProperty);
        set => SetValue(NumberBrushProperty, value);
    }

    /// <summary>
    ///     Foreground brush for line numbers.
    /// </summary>
    public IBrush? GutterBrush
    {
        get => GetValue(GutterBrushProperty);
        set => SetValue(GutterBrushProperty, value);
    }

    /// <summary>
    ///     Background highlight brush for error lines.
    /// </summary>
    public IBrush? ErrorBackgroundBrush
    {
        get => GetValue(ErrorBackgroundBrushProperty);
        set => SetValue(ErrorBackgroundBrushProperty, value);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        var lines = SplitLines(SourceText);
        var height = Math.Max(140, lines.Length * LINE_HEIGHT + 20);
        var width = double.IsInfinity(availableSize.Width) ? 620 : availableSize.Width;
        return new Size(width, height);
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var lines = SplitLines(SourceText);
        var mono = new Typeface("Liberation Mono, Monospace");
        var monoBold = new Typeface("Liberation Mono, Monospace", FontStyle.Normal, FontWeight.SemiBold);

        var textBrush = TextBrush ?? Brushes.White;
        var commentBrush = CommentBrush ?? Brushes.Gray;
        var sectionBrush = SectionBrush ?? Brushes.CornflowerBlue;
        var keyBrush = KeyBrush ?? Brushes.SkyBlue;
        var stringBrush = StringBrush ?? Brushes.MediumAquamarine;
        var numberBrush = NumberBrush ?? Brushes.Goldenrod;
        var gutterBrush = GutterBrush ?? Brushes.SlateGray;
        var errBg = ErrorBackgroundBrush ?? new SolidColorBrush(Color.FromArgb(60, 248, 113, 113));

        for (var i = 0; i < lines.Length; i++)
        {
            var lineNum = i + 1;
            var y = 10 + i * LINE_HEIGHT;
            var line = lines[i];

            if (lineNum == ErrorLineNumber || line.Contains("thruster_nozzle", StringComparison.Ordinal))
            {
                context.DrawRectangle(errBg, null, new Rect(0, y - 1, Bounds.Width, LINE_HEIGHT));
            }

            var gutterText = new FormattedText(
                lineNum.ToString(CultureInfo.InvariantCulture).PadLeft(3),
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                mono,
                11.5,
                gutterBrush);
            context.DrawText(gutterText, new Point(8, y + 1));

            var trimmed = line.TrimStart();
            if (trimmed.StartsWith('#'))
            {
                DrawToken(context, line, GUTTER_WIDTH, y, mono, commentBrush);
            }
            else if (trimmed.StartsWith('['))
            {
                DrawToken(context, line, GUTTER_WIDTH, y, monoBold, sectionBrush);
            }
            else
            {
                var eqIndex = line.IndexOf('=');
                if (eqIndex > 0)
                {
                    var keyPart = line[..eqIndex];
                    var eqAndVal = line[eqIndex..];
                    var x = GUTTER_WIDTH;
                    x += DrawToken(context, keyPart, x, y, mono, keyBrush);
                    var valTrim = eqAndVal.TrimStart('=').TrimStart();
                    var valBrush = valTrim.StartsWith('"') || valTrim.StartsWith('[')
                        ? stringBrush
                        : (valTrim.StartsWith('{') ? textBrush : numberBrush);
                    DrawToken(context, "= ", x, y, mono, textBrush);
                    DrawToken(context, eqAndVal[1..].TrimStart(), x + 15, y, mono, valBrush);
                }
                else
                {
                    var fallbackBrush = trimmed.StartsWith('"') ? stringBrush : textBrush;
                    DrawToken(context, line, GUTTER_WIDTH, y, mono, fallbackBrush);
                }
            }
        }
    }

    private static double DrawToken(DrawingContext context, string text, double x, double y, Typeface typeface, IBrush brush)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        var formatted = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            typeface,
            12,
            brush);
        context.DrawText(formatted, new Point(x, y + 1));
        return formatted.WidthIncludingTrailingWhitespace;
    }

    private static string[] SplitLines(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [string.Empty];
        }

        return text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
    }
}
