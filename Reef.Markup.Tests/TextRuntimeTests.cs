using Reef;
using Reef.UI;

namespace Reef.Markup.Tests;

public class TextRuntimeTests
{
    [Fact]
    public void TextFormsAndInterpolationRender()
    {
        var ui = new UIBuilder<PixelTexture>(400, 400, new StubTextEngine(), new UIController());
        var view = new TextFeaturesView();
        var context = new TextContext();

        view.Render(ui, context, null, null, null);
        List<RenderCommand<PixelTexture>> commands = ui.Build(0d);

        Assert.Contains(commands, command => command.Text == "Hello");
        Assert.Contains(commands, command => command.Text == "Body");
        Assert.Contains(commands, command => command.Text == "Attr");
        Assert.DoesNotContain(commands, command => command.Text == "Body ignored");
        Assert.Contains(commands, command => command.Text == "  Spaced  ");
        Assert.Contains(commands, command => command.Text == "Level 4 - Gold: 120");
        Assert.Contains(commands, command => command.Text == "Braced: {escaped} and x");
        Assert.Contains(commands, command => command.Text == "#FF0000Red #RReset");

        RenderCommand<PixelTexture> right = commands.Single(command => command.Text == "Right");
        Assert.Equal(HorizontalAlignment.Right, right.FontOptions.HorizontalAlignment);
        Assert.Equal(VerticalAlignment.Center, right.FontOptions.VerticalAlignment);
        Assert.True(right.FontOptions.NoWrap);
        Assert.Equal(100, right.Rect.Size.X);
        Assert.Equal(40, right.Rect.Size.Y);

        RenderCommand<PixelTexture> font = commands.Single(command => command.Text == "Font");
        Assert.Equal("salmon-mono-9-regular", font.FontOptions.ID);
        Assert.Equal(12, font.FontOptions.Size);
    }
}
