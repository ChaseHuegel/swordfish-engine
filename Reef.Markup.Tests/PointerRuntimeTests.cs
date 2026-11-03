using Reef;
using Reef.UI;

namespace Reef.Markup.Tests;

public partial class PointerView
{
    public int LeftCount { get; private set; }
    public int RightCount { get; private set; }
    public int MiddleCount { get; private set; }
    public int HeldCount { get; private set; }
    public int RightFilterCount { get; private set; }
    public int DisabledCount { get; private set; }

    partial void OnLeft() => LeftCount++;
    partial void OnRight() => RightCount++;
    partial void OnMiddle() => MiddleCount++;
    partial void OnHeld() => HeldCount++;
    partial void OnRightFilter() => RightFilterCount++;
    partial void OnDisabled() => DisabledCount++;
}

public class PointerRuntimeTests
{
    [Fact]
    public void PointerButtonsHeldAndDisabledAreRouted()
    {
        var controller = new UIController();
        var ui = new UIBuilder<PixelTexture>(400, 400, new StubTextEngine(), controller);
        var view = new PointerView();
        var context = new PointerContext();

        view.Render(ui, context, null, null, null);
        ui.Build(0d);

        Click(controller, ui, view, context, "left", UIController.MouseButtons.Left);
        Assert.Equal(1, view.LeftCount);

        Click(controller, ui, view, context, "right", UIController.MouseButtons.Right);
        Assert.Equal(1, view.RightCount);

        Click(controller, ui, view, context, "middle", UIController.MouseButtons.Middle);
        Assert.Equal(1, view.MiddleCount);

        Click(controller, ui, view, context, "right_filter", UIController.MouseButtons.Right);
        Assert.Equal(1, view.RightFilterCount);

        context.Disabled = true;
        Click(controller, ui, view, context, "disabled", UIController.MouseButtons.Left);
        Assert.Equal(0, view.DisabledCount);

        ElementBounds held = ui.GetElementBounds("held");
        int x = held.Rect.Left + 2;
        int y = held.Rect.Top + 2;
        controller.UpdateMouse(x, y, UIController.MouseButtons.None);
        view.Render(ui, context, null, null, null);
        ui.Build(0.1d);
        controller.UpdateMouse(x, y, UIController.MouseButtons.Left);
        view.Render(ui, context, null, null, null);
        ui.Build(0.1d);
        controller.UpdateMouse(x, y, UIController.MouseButtons.Left);
        view.Render(ui, context, null, null, null);
        ui.Build(0.1d);
        controller.UpdateMouse(x, y, UIController.MouseButtons.Left);
        view.Render(ui, context, null, null, null);
        ui.Build(0.1d);
        Assert.True(view.HeldCount > 0);
    }

    private static void Click(
        UIController controller,
        UIBuilder<PixelTexture> ui,
        PointerView view,
        PointerContext context,
        string id,
        UIController.MouseButtons button)
    {
        ElementBounds bounds = ui.GetElementBounds(id);
        int x = bounds.Rect.Left + 2;
        int y = bounds.Rect.Top + 2;

        controller.UpdateMouse(x, y, UIController.MouseButtons.None);
        view.Render(ui, context, null, null, null);
        ui.Build(0.1d);

        controller.UpdateMouse(x, y, button);
        view.Render(ui, context, null, null, null);
        ui.Build(0.1d);

        controller.UpdateMouse(x, y, UIController.MouseButtons.None);
        view.Render(ui, context, null, null, null);
        ui.Build(0.1d);
    }
}
