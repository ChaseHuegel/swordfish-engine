using System.Numerics;
using Reef;
using Reef.Markup.TestComponents;
using Reef.UI;

namespace Reef.Markup.Tests;

public partial class ModalView
{
    public int CloseCount { get; private set; }

    partial void OnClose()
    {
        CloseCount++;
    }
}

public class ComponentSlotTests
{
    [Fact]
    public void ReferencedComponentRendersCallerSlotsAndStyle()
    {
        var ui = new UIBuilder<PixelTexture>(500, 400, new StubTextEngine(), new UIController());
        var view = new ModalView();
        var context = new ModalState { Title = "Confirm" };

        view.Render(ui, context, null, null, null);
        List<RenderCommand<PixelTexture>> commands = ui.Build(0d);

        Assert.Contains(commands, command => command.Text == "Confirm");
        Assert.Contains(commands, command => command.Text == "Body from caller");
        Assert.Contains(commands, command => command.Text == "Close");

        RenderCommand<PixelTexture> modal = commands.First(command => command.ID != null && command.ID.Contains("__rml_"));
        Assert.Equal(0.1333333f, modal.Color.X, 3);
        Assert.Equal(0.1568628f, modal.Color.Y, 3);
        Assert.Equal(0.1921569f, modal.Color.Z, 3);
        Assert.Equal(1f, modal.Color.W, 3);
    }

    [Fact]
    public void SlotCallbackInvokesCompanionPartial()
    {
        var ui = new UIBuilder<PixelTexture>(500, 400, new StubTextEngine(), new UIController());
        var view = new ModalView();
        var context = new ModalState { Title = "Confirm" };

        ui.Build(0d);
        view.Render(ui, context, null, null, null);
        ui.Build(0d);
        ElementBounds bounds = ui.GetElementBounds("close_button");
        int x = bounds.Rect.Position.X + bounds.Rect.Size.X / 2;
        int y = bounds.Rect.Position.Y + bounds.Rect.Size.Y / 2;

        ui = new UIBuilder<PixelTexture>(500, 400, new StubTextEngine(), new UIController());
        var controller = new UIController();
        ui = new UIBuilder<PixelTexture>(500, 400, new StubTextEngine(), controller);

        controller.UpdateMouse(x, y, UIController.MouseButtons.None);
        view.Render(ui, context, null, null, null);
        ui.Build(0d);

        controller.UpdateMouse(x, y, UIController.MouseButtons.Left);
        view.Render(ui, context, null, null, null);
        ui.Build(0d);

        controller.UpdateMouse(x, y, UIController.MouseButtons.None);
        view.Render(ui, context, null, null, null);
        ui.Build(0d);

        Assert.Equal(1, view.CloseCount);
    }
}
