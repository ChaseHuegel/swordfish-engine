using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace WaywardBeyond.AssetStudio.Controls;

/// <summary>
///     Native Avalonia color picker with 2D HSV plane, hue spectrum bar, RGB sliders, hex input, and curated swatches.
/// </summary>
public sealed partial class StudioColorPickerControl : UserControl
{
    /// <summary>
    ///     Styled property binding the selected color as a #RRGGBB hex string.
    /// </summary>
    public static readonly StyledProperty<string> HexColorProperty =
        AvaloniaProperty.Register<StudioColorPickerControl, string>(
            nameof(HexColor),
            defaultValue: "#FFFFFF",
            defaultBindingMode: BindingMode.TwoWay);

    private static readonly (string Name, string Hex)[] CURATED_SWATCHES =
    [
        ("White", "#FFFFFF"),
        ("Silver", "#C0CBDC"),
        ("Steel", "#8B9BB4"),
        ("Slate", "#5A6988"),
        ("Indigo", "#3A4466"),
        ("Obsidian", "#181425"),
        ("Hull Blue", "#4A6B8A"),
        ("Azure", "#0099DB"),
        ("Cyan", "#2CE8F5"),
        ("Red", "#E43B44"),
        ("Amber", "#FEAE34"),
        ("Lime", "#63C74D"),
    ];

    private readonly List<(Button Button, string Hex)> _swatchButtons = [];
    private double _hue = 215.0;
    private double _saturation;
    private double _value = 1.0;
    private Color _currentColor = Colors.White;
    private bool _isSyncing;

    /// <summary>
    ///     Initializes the color picker control and wires its interactive parts.
    /// </summary>
    public StudioColorPickerControl()
    {
        InitializeComponent();
        BuildCuratedSwatches();

        PART_SvSquare.ValueEdited += HandleSvEdited;
        PART_HueBar.ValueEdited += HandleHueEdited;
        PART_RedSlider.ValueChanged += HandleRgbSliderChanged;
        PART_GreenSlider.ValueChanged += HandleRgbSliderChanged;
        PART_BlueSlider.ValueChanged += HandleRgbSliderChanged;
        PART_HexInput.TextChanged += HandleHexTextChanged;
        PART_HexInput.LostFocus += HandleHexLostFocus;
        PART_HexInput.KeyDown += HandleHexKeyDown;

        SyncFromExternalHex(HexColor);
    }

    /// <summary>
    ///     Selected color formatted as an uppercase #RRGGBB hex string.
    /// </summary>
    public string HexColor
    {
        get => GetValue(HexColorProperty);
        set => SetValue(HexColorProperty, value);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HexColorProperty && !_isSyncing)
        {
            SyncFromExternalHex(change.NewValue as string);
        }
    }

    internal static Color HsvToRgb(double hue, double saturation, double value)
    {
        var h = ((hue % 360.0) + 360.0) % 360.0;
        var s = Math.Clamp(saturation, 0.0, 1.0);
        var v = Math.Clamp(value, 0.0, 1.0);

        var c = v * s;
        var x = c * (1.0 - Math.Abs((h / 60.0) % 2.0 - 1.0));
        var m = v - c;

        double r1;
        double g1;
        double b1;
        if (h < 60.0)
        {
            (r1, g1, b1) = (c, x, 0.0);
        }
        else if (h < 120.0)
        {
            (r1, g1, b1) = (x, c, 0.0);
        }
        else if (h < 180.0)
        {
            (r1, g1, b1) = (0.0, c, x);
        }
        else if (h < 240.0)
        {
            (r1, g1, b1) = (0.0, x, c);
        }
        else if (h < 300.0)
        {
            (r1, g1, b1) = (x, 0.0, c);
        }
        else
        {
            (r1, g1, b1) = (c, 0.0, x);
        }

        var r = (byte)Math.Clamp((int)Math.Round((r1 + m) * 255.0), 0, 255);
        var g = (byte)Math.Clamp((int)Math.Round((g1 + m) * 255.0), 0, 255);
        var b = (byte)Math.Clamp((int)Math.Round((b1 + m) * 255.0), 0, 255);
        return Color.FromRgb(r, g, b);
    }

    internal static (double Hue, double Saturation, double Value) RgbToHsv(Color color, double fallbackHue)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        var v = max;
        var s = max <= 0.0 ? 0.0 : delta / max;

        double h;
        if (delta <= 1e-6)
        {
            h = fallbackHue;
        }
        else if (Math.Abs(max - r) <= 1e-6)
        {
            h = 60.0 * (((g - b) / delta) % 6.0);
        }
        else if (Math.Abs(max - g) <= 1e-6)
        {
            h = 60.0 * (((b - r) / delta) + 2.0);
        }
        else
        {
            h = 60.0 * (((r - g) / delta) + 4.0);
        }

        if (h < 0.0)
        {
            h += 360.0;
        }

        return (h, s, v);
    }

    private void BuildCuratedSwatches()
    {
        PART_SwatchesRow.Children.Clear();
        _swatchButtons.Clear();

        for (var i = 0; i < CURATED_SWATCHES.Length; i++)
        {
            var entry = CURATED_SWATCHES[i];
            var button = new Button
            {
                Background = new SolidColorBrush(Color.Parse(entry.Hex)),
                Tag = entry.Hex,
            };
            button.Classes.Add("studio-palette-swatch");
            ToolTip.SetTip(button, $"{entry.Name} ({entry.Hex})");
            button.Click += HandleSwatchClicked;

            PART_SwatchesRow.Children.Add(button);
            _swatchButtons.Add((button, entry.Hex));
        }
    }

    private void SyncFromExternalHex(string? hexText)
    {
        if (!TryParseHexColor(hexText, allowShortForm: true, out var parsed))
        {
            parsed = Colors.White;
        }

        _isSyncing = true;
        try
        {
            _currentColor = parsed;
            (_hue, _saturation, _value) = RgbToHsv(parsed, _hue);
            ApplyVisualState(updateHexInput: true);
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void HandleSvEdited(object? sender, EventArgs e)
    {
        if (_isSyncing)
        {
            return;
        }

        _isSyncing = true;
        try
        {
            _saturation = PART_SvSquare.Saturation;
            _value = PART_SvSquare.Value;
            _currentColor = HsvToRgb(_hue, _saturation, _value);
            ApplyVisualState(updateHexInput: true);
            CommitHexColor();
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void HandleHueEdited(object? sender, EventArgs e)
    {
        if (_isSyncing)
        {
            return;
        }

        _isSyncing = true;
        try
        {
            _hue = PART_HueBar.Hue;
            _currentColor = HsvToRgb(_hue, _saturation, _value);
            ApplyVisualState(updateHexInput: true);
            CommitHexColor();
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void HandleRgbSliderChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isSyncing)
        {
            return;
        }

        _isSyncing = true;
        try
        {
            var r = (byte)Math.Clamp((int)Math.Round(PART_RedSlider.Value), 0, 255);
            var g = (byte)Math.Clamp((int)Math.Round(PART_GreenSlider.Value), 0, 255);
            var b = (byte)Math.Clamp((int)Math.Round(PART_BlueSlider.Value), 0, 255);
            _currentColor = Color.FromRgb(r, g, b);
            (_hue, _saturation, _value) = RgbToHsv(_currentColor, _hue);
            ApplyVisualState(updateHexInput: true);
            CommitHexColor();
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void HandleHexTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isSyncing)
        {
            return;
        }

        if (!TryParseHexColor(PART_HexInput.Text, allowShortForm: false, out var parsed))
        {
            return;
        }

        _isSyncing = true;
        try
        {
            _currentColor = parsed;
            (_hue, _saturation, _value) = RgbToHsv(parsed, _hue);
            ApplyVisualState(updateHexInput: false);
            CommitHexColor();
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void HandleHexLostFocus(object? sender, RoutedEventArgs e)
    {
        NormalizeHexInput();
    }

    private void HandleHexKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            NormalizeHexInput();
            e.Handled = true;
        }
    }

    private void NormalizeHexInput()
    {
        if (_isSyncing)
        {
            return;
        }

        _isSyncing = true;
        try
        {
            if (TryParseHexColor(PART_HexInput.Text, allowShortForm: true, out var parsed))
            {
                _currentColor = parsed;
                (_hue, _saturation, _value) = RgbToHsv(parsed, _hue);
                CommitHexColor();
            }

            ApplyVisualState(updateHexInput: true);
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void HandleSwatchClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string hex } || !TryParseHexColor(hex, allowShortForm: true, out var parsed))
        {
            return;
        }

        _isSyncing = true;
        try
        {
            _currentColor = parsed;
            (_hue, _saturation, _value) = RgbToHsv(parsed, _hue);
            ApplyVisualState(updateHexInput: true);
            CommitHexColor();
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void ApplyVisualState(bool updateHexInput)
    {
        var formattedHex = FormatHex(_currentColor);

        PART_SvSquare.Hue = _hue;
        PART_SvSquare.Saturation = _saturation;
        PART_SvSquare.Value = _value;
        PART_HueBar.Hue = _hue;

        PART_PreviewSwatch.Background = new SolidColorBrush(_currentColor);

        PART_RedSlider.Value = _currentColor.R;
        PART_GreenSlider.Value = _currentColor.G;
        PART_BlueSlider.Value = _currentColor.B;

        PART_RedValue.Text = _currentColor.R.ToString(CultureInfo.InvariantCulture);
        PART_GreenValue.Text = _currentColor.G.ToString(CultureInfo.InvariantCulture);
        PART_BlueValue.Text = _currentColor.B.ToString(CultureInfo.InvariantCulture);

        if (updateHexInput)
        {
            PART_HexInput.Text = formattedHex;
        }

        for (var i = 0; i < _swatchButtons.Count; i++)
        {
            var (button, swatchHex) = _swatchButtons[i];
            var isMatch = string.Equals(swatchHex, formattedHex, StringComparison.OrdinalIgnoreCase);
            button.Classes.Set("active", isMatch);
        }
    }

    private void CommitHexColor()
    {
        var next = FormatHex(_currentColor);
        if (!string.Equals(HexColor, next, StringComparison.OrdinalIgnoreCase))
        {
            SetCurrentValue(HexColorProperty, next);
        }
    }

    private static string FormatHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static bool TryParseHexColor(string? input, bool allowShortForm, out Color color)
    {
        color = Colors.White;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var span = input.AsSpan().Trim();
        if (span.Length > 0 && span[0] == '#')
        {
            span = span[1..];
        }

        if (span.Length == 6 && uint.TryParse(span, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
        {
            color = Color.FromRgb(
                (byte)((rgb >> 16) & 0xFF),
                (byte)((rgb >> 8) & 0xFF),
                (byte)(rgb & 0xFF));
            return true;
        }

        if (span.Length == 8 && uint.TryParse(span, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
        {
            color = Color.FromRgb(
                (byte)((argb >> 16) & 0xFF),
                (byte)((argb >> 8) & 0xFF),
                (byte)(argb & 0xFF));
            return true;
        }

        if (allowShortForm
            && span.Length == 3
            && uint.TryParse(span, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var shortRgb))
        {
            var r = (byte)(((shortRgb >> 8) & 0xF) * 17);
            var g = (byte)(((shortRgb >> 4) & 0xF) * 17);
            var b = (byte)((shortRgb & 0xF) * 17);
            color = Color.FromRgb(r, g, b);
            return true;
        }

        return false;
    }
}
