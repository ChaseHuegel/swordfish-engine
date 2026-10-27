using Reef;
using Reef.Text;
using Reef.UI;

namespace Reef.Markup.Tests;

internal sealed class StubTextEngine : ITextEngine
{
    public ITypeface GetDefaultTypeface() => null!;

    public ITypeface[] GetTypefaces() => [];

    public bool TryGetTypeface(FontInfo fontInfo, out ITypeface? typeface)
    {
        typeface = null;
        return false;
    }

    public TextConstraints Measure(FontOptions fontOptions, string text) => MeasureText(fontOptions, text);

    public TextConstraints Measure(FontOptions fontOptions, string text, int start, int length)
    {
        return MeasureText(fontOptions, text.Substring(start, length));
    }

    public TextLayout Layout(FontOptions fontOptions, string text) => LayoutText(fontOptions, text);

    public TextLayout Layout(FontOptions fontOptions, string text, int maxWidth) => LayoutText(fontOptions, text);

    public TextLayout Layout(FontOptions fontOptions, string text, int start, int length)
    {
        return LayoutText(fontOptions, text.Substring(start, length));
    }

    public TextLayout Layout(FontOptions fontOptions, string text, int start, int length, int maxWidth)
    {
        return LayoutText(fontOptions, text.Substring(start, length));
    }

    public string[] Wrap(FontOptions fontOptions, string text, int maxWidth) => [text];

    public string[] Wrap(FontOptions fontOptions, string text, int start, int length, int maxWidth)
    {
        return [text.Substring(start, length)];
    }

    private static TextConstraints MeasureText(FontOptions fontOptions, string text)
    {
        int width = text.Length * 8;
        int height = Math.Max(1, fontOptions.Size);
        return new TextConstraints(width, height, width, height);
    }

    private static TextLayout LayoutText(FontOptions fontOptions, string text)
    {
        return new TextLayout(MeasureText(fontOptions, text), [], [], Math.Max(1, fontOptions.Size));
    }
}
