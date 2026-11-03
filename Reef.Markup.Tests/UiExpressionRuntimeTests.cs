using System.Numerics;
using Reef;
using Reef.UI;

namespace Reef.Markup.Tests;

public partial class UiExpressionsView
{
    public int InteractCount { get; private set; }

    protected partial string Localize(string key) => "Localized " + key;

    protected partial string FormatProgress(int cooldown, double delta) => "Progress " + cooldown + " " + delta.ToString("0.0");

    protected partial Vector4 GetPulseColor(int rarity, double time) => new(rarity, (float)time, 0.5f, 1f);

    protected partial UIController.Key GetKeyBinding(string action) => UIController.Key.E;

    partial void OnInteract()
    {
        InteractCount++;
    }
}

public class UiExpressionRuntimeTests
{
    [Fact]
    public void UiEnvironmentAndHelpersRender()
    {
        var ui = new UIBuilder<PixelTexture>(420, 300, new StubTextEngine(), new UIController());
        var view = new UiExpressionsView();
        var context = new UiContext();

        view.Render(ui, context, null, null, null);
        ui.Build(0.1d);
        view.Render(ui, context, null, null, null);
        List<RenderCommand<PixelTexture>> commands = ui.Build(0.1d);

        Assert.Contains(commands, command => command.Text == "Time 0.1");
        Assert.Contains(commands, command => command.Text == "Delta 0.1");
        Assert.Contains(commands, command => command.Text == "Size 420x300");
        Assert.Contains(commands, command => command.Text == "Bounds 20");
        Assert.Contains(commands, command => command.Text == "Scroll 0");
        Assert.Contains(commands, command => command.Text == "Localized ui.menu.title");
        Assert.Contains(commands, command => command.Text == "Progress 5 0.1");
        Assert.Contains(commands, command => command.Text == "Score 3 at 0.1");

        RenderCommand<PixelTexture> icon = commands.Single(command => command.RendererData != null);
        Assert.Equal(2f, icon.Color.X, 3);
        Assert.Equal(0.1f, icon.Color.Y, 3);
    }
}
