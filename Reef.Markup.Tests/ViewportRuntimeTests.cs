using System.Numerics;
using Reef;
using Reef.UI;

namespace Reef.Markup.Tests;

public partial class OverlayView
{
    public int BackCount { get; private set; }
    public float LastScrollDelta { get; private set; }

    partial void OnBack()
    {
        BackCount++;
    }

    partial void OnScrolled(float delta)
    {
        LastScrollDelta = delta;
    }
}

public class ViewportRuntimeTests
{
    [Fact]
    public void ClipOverlayAndPassthroughBehavior()
    {
        var controller = new UIController();
        var ui = new UIBuilder<PixelTexture>(400, 300, new StubTextEngine(), controller);
        var view = new OverlayView();
        var context = new OverlayContext();
        view.Render(ui, context, null, null, null);
        List<RenderCommand<PixelTexture>> commands = ui.Build(0d);

        RenderCommand<PixelTexture> parent = commands.Single(command => command.ID == "clip_parent");
        RenderCommand<PixelTexture> overlay = commands.Single(command => command.ID == "overlay");
        Assert.True(overlay.Rect.Right > parent.Rect.Right);
        Assert.True(overlay.ClipRect.Right >= overlay.Rect.Right);

        ElementBounds bottom = ui.GetElementBounds("bottom");
        int x = bottom.Rect.Left + 4;
        int y = bottom.Rect.Top + 4;
        controller.UpdateMouse(x, y, UIController.MouseButtons.None);
        view.Render(ui, context, null, null, null);
        ui.Build(0.1d);
        controller.UpdateMouse(x, y, UIController.MouseButtons.Left);
        view.Render(ui, context, null, null, null);
        ui.Build(0.1d);
        controller.UpdateMouse(x, y, UIController.MouseButtons.None);
        view.Render(ui, context, null, null, null);
        ui.Build(0.1d);
        Assert.Equal(1, view.BackCount);
    }

    [Fact]
    public void ScrollOffsetAndCallbackWork()
    {
        var controller = new UIController();
        var ui = new UIBuilder<PixelTexture>(400, 300, new StubTextEngine(), controller);
        var view = new OverlayView();
        var context = new OverlayContext { ScrollY = -10 };
        view.Render(ui, context, null, null, null);
        List<RenderCommand<PixelTexture>> commands = ui.Build(0d);

        RenderCommand<PixelTexture> scroll = commands.Single(command => command.ID == "scroll");
        Assert.Equal(70, scroll.Rect.Top);
        RenderCommand<PixelTexture> content = commands.Single(command => command.ID == "scroll_content");
        Assert.Equal(scroll.Rect.Top - 10, content.Rect.Top);

        ElementBounds bounds = ui.GetElementBounds("scroll");
        controller.UpdateMouse(bounds.Rect.Left + 5, bounds.Rect.Top + 5, UIController.MouseButtons.None, scrollDeltaY: 2.5f);
        view.Render(ui, context, null, null, null);
        ui.Build(0.1d);
        controller.UpdateMouse(bounds.Rect.Left + 5, bounds.Rect.Top + 5, UIController.MouseButtons.None, scrollDeltaY: 2.5f);
        view.Render(ui, context, null, null, null);
        ui.Build(0.1d);
        Assert.Equal(2.5f, view.LastScrollDelta, 3);
    }

    [Fact]
    public void VisibleFalseSkipsElement()
    {
        var ui = new UIBuilder<PixelTexture>(400, 300, new StubTextEngine(), new UIController());
        var view = new OverlayView();
        var context = new OverlayContext { Hide = false };
        view.Render(ui, context, null, null, null);
        Assert.DoesNotContain(ui.Build(0d), command => command.ID == "hidden");

        context.Hide = true;
        ui = new UIBuilder<PixelTexture>(400, 300, new StubTextEngine(), new UIController());
        view.Render(ui, context, null, null, null);
        Assert.Contains(ui.Build(0d), command => command.ID == "hidden");
    }
}
