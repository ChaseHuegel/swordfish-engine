using Reef.Constraints;
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

    [Fact]
    public void WrapHandlesOverWidthAndNewlineWithoutReordering()
    {
        TextEngine textEngine = CreateTextEngine();
        var fontOptions = new FontOptions
        {
            ID = "salmon-mono-9-regular",
            Size = 16,
        };

        TextLayout layout = textEngine.Layout(fontOptions, "Hello world\nNext", maxWidth: 83);

        Assert.Equal(["Hello", "world", "Next"], layout.Lines);
    }

    [Fact]
    public void WrapHandlesCarriageReturnAndConsecutiveNewlines()
    {
        TextEngine textEngine = CreateTextEngine();
        var fontOptions = new FontOptions
        {
            ID = "salmon-mono-9-regular",
            Size = 16,
        };

        TextLayout layout = textEngine.Layout(fontOptions, "A\r\n\r\nB\n", maxWidth: 200);

        Assert.Equal(["A", "", "B", ""], layout.Lines);
        Assert.Equal(2, layout.Glyphs.Length);
    }

    [Fact]
    public void HorizontalAlignmentOffsetsShorterLines()
    {
        TextEngine textEngine = CreateTextEngine();
        var leftOptions = new FontOptions
        {
            ID = "salmon-mono-9-regular",
            Size = 16,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        var centerOptions = leftOptions with { HorizontalAlignment = HorizontalAlignment.Center };
        var rightOptions = leftOptions with { HorizontalAlignment = HorizontalAlignment.Right };

        TextLayout leftLayout = textEngine.Layout(leftOptions, "Hello\nHi");
        TextLayout centerLayout = textEngine.Layout(centerOptions, "Hello\nHi");
        TextLayout rightLayout = textEngine.Layout(rightOptions, "Hello\nHi");

        //  Glyph index 5 is 'H' on the second line ("Hi")
        Assert.True(centerLayout.Glyphs[5].BBOX.Left > leftLayout.Glyphs[5].BBOX.Left);
        Assert.True(rightLayout.Glyphs[5].BBOX.Left > centerLayout.Glyphs[5].BBOX.Left);
    }

    [Fact]
    public void TextElementPreservesExplicitConstraintsAndAlignsText()
    {
        TextEngine textEngine = CreateTextEngine();
        var controller = new UIController();
        var ui = new UIBuilder<PixelTexture>(200, 100, textEngine, controller);

        using (ui.Text("Hi"))
        {
            ui.ID = "label";
            ui.HorizontalAlignment = HorizontalAlignment.Center;
            ui.VerticalAlignment = VerticalAlignment.Center;
            ui.Constraints = new UI.Constraints
            {
                Width = new Fixed(120),
                Height = new Fixed(60),
            };
        }

        List<RenderCommand<PixelTexture>> commands = ui.Build(delta: 0f);
        RenderCommand<PixelTexture> command = Assert.Single(commands);

        Assert.Equal(120, command.Rect.Size.X);
        Assert.Equal(60, command.Rect.Size.Y);

        TextLayout layout = ui.GetTextLayout("label");
        Assert.True(layout.Glyphs[0].BBOX.Left > 0);
        Assert.True(layout.Glyphs[0].BBOX.Top > 0);
    }

    [Fact]
    public void RichTextTagsDoNotZeroOutFirstWordMinWidth()
    {
        TextEngine textEngine = CreateTextEngine();
        var controller = new UIController();
        var ui = new UIBuilder<PixelTexture>(200, 100, textEngine, controller);

        using (ui.Element("container"))
        {
            ui.Constraints = new UI.Constraints
            {
                Width = new Fixed(10),
                Height = new Fixed(100),
            };

            using (ui.Text("#red Hello world"))
            {
                ui.ID = "rich";
            }
        }

        List<RenderCommand<PixelTexture>> commands = ui.Build(delta: 0f);
        RenderCommand<PixelTexture> textCommand = commands[1];

        //  "Hello" is 5 chars (~53px), so MinWidth prevents shrinking down to 10px
        Assert.True(textCommand.Rect.Size.X >= 50);
    }

    private static TextEngine CreateTextEngine()
    {
        var font = new FontInfo("salmon-mono-9-regular", "TestFiles/Fonts/Salmon Mono 9 Regular.ttf");
        return new TextEngine([font]);
    }
}

