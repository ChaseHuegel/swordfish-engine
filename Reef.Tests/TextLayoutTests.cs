using Reef.MSDF;
using Reef.Text;
using Reef.UI;

namespace Reef.Tests;

[Collection("TextEngine")]
public class TextLayoutTests
{
    [Fact]
    public void NoWrapKeepsTextOnOneLine()
    {
        TextEngine textEngine = CreateTextEngine();
        var fontOptions = new FontOptions
        {
            ID = "salmon-mono-9-regular",
            Size = 16,
            NoWrap = true,
        };

        TextLayout layout = textEngine.Layout(fontOptions, "Hello world!", maxWidth: 10);

        Assert.Single(layout.Lines);
    }

    [Fact]
    public void DefaultLayoutWrapsTextToMaxWidth()
    {
        TextEngine textEngine = CreateTextEngine();
        var fontOptions = new FontOptions
        {
            ID = "salmon-mono-9-regular",
            Size = 16,
        };

        TextLayout layout = textEngine.Layout(fontOptions, "Hello world!", maxWidth: 10);

        Assert.True(layout.Lines.Length > 1);
    }

    private static TextEngine CreateTextEngine()
    {
        var font = new FontInfo("salmon-mono-9-regular", "TestFiles/Fonts/Salmon Mono 9 Regular.ttf");
        return new TextEngine([font]);
    }
}
