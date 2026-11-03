using System.Numerics;
using Reef;
using Reef.UI;

namespace Reef.Markup.Tests;

public class StyleRuntimeTests
{
    [Fact]
    public void StylesInheritOrderAndInlineOverride()
    {
        var ui = new UIBuilder<PixelTexture>(500, 400, new StubTextEngine(), new UIController());
        var view = new StyleView();
        var context = new StyleContext { Accent = true };
        view.Render(ui, context, null, null, null);
        List<RenderCommand<PixelTexture>> commands = ui.Build(0d);

        RenderCommand<PixelTexture> panel = commands.Single(command => command.ID == "panel");
        Assert.Equal(0.1254902f, panel.BackgroundColor.X, 3);
        Assert.Equal(1, panel.Border.Thickness);

        RenderCommand<PixelTexture> radius = commands.Single(command => command.ID == "radius");
        Assert.Equal(4, radius.CornerRadius.TopLeft);
        Assert.Equal(8, radius.CornerRadius.TopRight);
        Assert.Equal(16, radius.CornerRadius.BottomLeft);
        Assert.Equal(12, radius.CornerRadius.BottomRight);
        Assert.Equal(2, radius.Border.Thickness);
        Assert.Equal(10, radius.CornerCaps.Length);

        RenderCommand<PixelTexture> inline = commands.Single(command => command.ID == "inline");
        Assert.Equal(1f, inline.BackgroundColor.X, 3);

        RenderCommand<PixelTexture> order = commands.Single(command => command.ID == "order");
        Assert.Equal(1f, order.Color.Y, 3);
        Assert.Equal(0f, order.Color.X, 3);

        RenderCommand<PixelTexture> dynamic = commands.Single(command => command.ID == "dynamic");
        Assert.Equal(1f, dynamic.Color.X, 3);
        Assert.Equal(1f, dynamic.Color.Y, 3);
    }

    [Fact]
    public void PseudoStatesAndDynamicClassesApplyInPrecedenceOrder()
    {
        var controller = new UIController();
        var ui = new UIBuilder<PixelTexture>(500, 400, new StubTextEngine(), controller);
        var view = new StyleView();
        var context = new StyleContext { Accent = true };

        view.Render(ui, context, null, null, null);
        ui.Build(0d);
        ElementBounds panel = ui.GetElementBounds("panel");
        int x = panel.Rect.Left + 4;
        int y = panel.Rect.Top + 4;

        controller.UpdateMouse(x, y, UIController.MouseButtons.None);
        view.Render(ui, context, null, null, null);
        ui.Build(0d);
        controller.UpdateMouse(x, y, UIController.MouseButtons.None);
        view.Render(ui, context, null, null, null);
        List<RenderCommand<PixelTexture>> hovered = ui.Build(0.1d);
        RenderCommand<PixelTexture> hoverPanel = hovered.Single(command => command.ID == "panel");
        Assert.Equal(0.1882353f, hoverPanel.BackgroundColor.X, 3);
        Assert.Equal(0.1882353f, hoverPanel.Color.X, 3);

        controller.UpdateMouse(x, y, UIController.MouseButtons.Left);
        view.Render(ui, context, null, null, null);
        ui.Build(0.1d);
        controller.UpdateMouse(x, y, UIController.MouseButtons.Left);
        view.Render(ui, context, null, null, null);
        ui.Build(0.1d);
        controller.UpdateMouse(x, y, UIController.MouseButtons.Left);
        view.Render(ui, context, null, null, null);
        List<RenderCommand<PixelTexture>> active = ui.Build(0.1d);
        RenderCommand<PixelTexture> activePanel = active.Single(command => command.ID == "panel");
        Assert.Equal(0.2509804f, activePanel.BackgroundColor.X, 3);

        context.Disabled = true;
        controller.UpdateMouse(x, y, UIController.MouseButtons.None);
        view.Render(ui, context, null, null, null);
        List<RenderCommand<PixelTexture>> disabled = ui.Build(0.1d);
        RenderCommand<PixelTexture> disabledPanel = disabled.Single(command => command.ID == "panel");
        Assert.Equal(0.3137255f, disabledPanel.BackgroundColor.X, 3);
    }
}
