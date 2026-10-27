using System.Numerics;
using Reef;
using Reef.Markup.TestComponents;
using Reef.UI;

namespace Reef.Markup.Tests;

public class ComponentRuntimeTests
{
    [Fact]
    public void ReferencedComponentRenders()
    {
        var ui = new UIBuilder<PixelTexture>(400, 300, new StubTextEngine(), new UIController());
        var view = new PlayerView();
        var context = new PlayerInfo
        {
            Name = "Ada",
            Score = 42,
        };

        view.Render(ui, context, null, null, null);
        List<RenderCommand<PixelTexture>> commands = ui.Build(0d);

        Assert.Contains(commands, command => command.Text == "Ada");
        Assert.Contains(commands, command => command.Text == "Score: 42");

        RenderCommand<PixelTexture> card = commands.First(command => command.Text == null && command.Rect.Size.X == 240);
        Assert.Equal(2, card.Border.Thickness);
        Assert.Equal(7, card.CornerRadius.TopLeft);
    }

    [Fact]
    public void CallerOverridesApplyToComponentRoot()
    {
        var ui = new UIBuilder<PixelTexture>(400, 300, new StubTextEngine(), new UIController());
        var card = new PlayerCard();
        var context = new PlayerInfo { Name = "Ada", Score = 42 };
        var style = new StyleOverride
        {
            BackgroundColor = new Vector4(1f, 0f, 0f, 1f),
            CornerRadius = new CornerRadius(9),
        };
        var constraints = new ConstraintsOverride
        {
            Width = new Reef.Constraints.Fixed(300),
        };

        card.Render(ui, context, style, constraints, null);
        List<RenderCommand<PixelTexture>> commands = ui.Build(0d);

        RenderCommand<PixelTexture> root = commands.First(command => command.Text == null && command.Rect.Size.X == 300);
        Assert.Equal(1f, root.BackgroundColor.X, 3);
        Assert.Equal(9, root.CornerRadius.TopLeft);
    }
}
