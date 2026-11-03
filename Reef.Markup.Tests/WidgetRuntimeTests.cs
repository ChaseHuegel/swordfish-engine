using Reef;
using Reef.Markup.TestComponents;
using Reef.UI;

namespace Reef.Markup.Tests;

public partial class WidgetsView
{
    public int DecoupledChanges { get; private set; }
    public bool LastDecoupledValue { get; private set; }

    partial void OnDecoupledChanged(bool value)
    {
        DecoupledChanges++;
        LastDecoupledValue = value;
    }
}

public class WidgetRuntimeTests
{
    [Fact]
    public void WidgetsRenderAndBind()
    {
        var controller = new UIController();
        var ui = new UIBuilder<PixelTexture>(500, 400, new StubTextEngine(), controller);
        var view = new WidgetsView();
        var context = new WidgetState();

        view.Render(ui, context, null, null, null);
        List<RenderCommand<PixelTexture>> commands = ui.Build(0d);
        Assert.Contains(commands, command => command.Text == "Mute");
        Assert.Contains(commands, command => command.Text == "Off");
        Assert.Contains(commands, command => command.Text == "Inside section");
        Assert.Contains(commands, command => command.Text == "Wrapped");
        Assert.Contains(commands, command => command.ID == "section");

        ElementBounds checkbox = ui.GetElementBounds("mute");
        ClickAt(controller, ui, view, context, checkbox, 0d);
        Assert.True(context.Muted);

        ElementBounds toggle = ui.GetElementBounds("fullscreen");
        ClickAt(controller, ui, view, context, toggle, 0.1d);
        Assert.True(context.Fullscreen);

        ElementBounds chat = ui.GetElementBounds("chat");
        ClickAt(controller, ui, view, context, chat, 0.2d);
        Assert.Equal(1, context.Chat.Clicks);

        ElementBounds decoupled = ui.GetElementBounds("decoupled");
        ClickAt(controller, ui, view, context, decoupled, 0.3d);
        Assert.Equal(1, view.DecoupledChanges);
        Assert.True(view.LastDecoupledValue);
    }

    private static void ClickAt(
        UIController controller,
        UIBuilder<PixelTexture> ui,
        WidgetsView view,
        WidgetState context,
        ElementBounds bounds,
        double delta)
    {
        int x = bounds.Rect.Position.X + Math.Max(1, bounds.Rect.Size.X / 2);
        int y = bounds.Rect.Position.Y + Math.Max(1, bounds.Rect.Size.Y / 2);

        controller.UpdateMouse(x, y, UIController.MouseButtons.None);
        view.Render(ui, context, null, null, null);
        ui.Build(delta);

        controller.UpdateMouse(x, y, UIController.MouseButtons.Left);
        view.Render(ui, context, null, null, null);
        ui.Build(delta);

        controller.UpdateMouse(x, y, UIController.MouseButtons.None);
        view.Render(ui, context, null, null, null);
        ui.Build(delta);
    }
}
