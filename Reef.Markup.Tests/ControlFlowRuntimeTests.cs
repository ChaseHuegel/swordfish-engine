using Reef;
using Reef.Markup.TestComponents;
using Reef.UI;

namespace Reef.Markup.Tests;

public partial class ControlFlowView
{
    public int SelectedIndex { get; private set; } = -1;
    public string SelectedId { get; private set; } = string.Empty;
    public int HotkeyCount { get; private set; }

    partial void OnSelect(string id, int index)
    {
        SelectedId = id;
        SelectedIndex = index;
    }

    partial void OnHotkey()
    {
        HotkeyCount++;
    }
}

public class ControlFlowRuntimeTests
{
    [Fact]
    public void ConditionalBranchesRender()
    {
        var ui = new UIBuilder<PixelTexture>(400, 400, new StubTextEngine(), new UIController());
        var view = new ControlFlowView();
        var context = new FlowContext { Loading = true };

        view.Render(ui, context, null, null, null);
        List<RenderCommand<PixelTexture>> commands = ui.Build(0d);
        Assert.Contains(commands, command => command.Text == "Loading");
        Assert.DoesNotContain(commands, command => command.Text == "Ready");

        context.Loading = false;
        ui = new UIBuilder<PixelTexture>(400, 400, new StubTextEngine(), new UIController());
        view.Render(ui, context, null, null, null);
        commands = ui.Build(0d);
        Assert.Contains(commands, command => command.Text == "Ready");
    }

    [Fact]
    public void EmptyStateAndNestedLoopsRender()
    {
        var ui = new UIBuilder<PixelTexture>(400, 400, new StubTextEngine(), new UIController());
        var view = new ControlFlowView();
        var context = new FlowContext();

        view.Render(ui, context, null, null, null);
        List<RenderCommand<PixelTexture>> commands = ui.Build(0d);
        Assert.Contains(commands, command => command.Text == "No items");

        context.Items =
        [
            new FlowItem { Id = "a", Name = "Alpha" },
            new FlowItem { Id = "b", Name = "Beta" },
        ];
        context.Groups =
        [
            new FlowGroup
            {
                Id = "g1",
                Items =
                [
                    new FlowItem { Id = "c", Name = "Gamma" },
                ],
            },
        ];

        ui = new UIBuilder<PixelTexture>(400, 400, new StubTextEngine(), new UIController());
        view.Render(ui, context, null, null, null);
        commands = ui.Build(0d);
        Assert.Contains(commands, command => command.Text == "#0: Alpha");
        Assert.Contains(commands, command => command.Text == "#1: Beta");
        Assert.Contains(commands, command => command.Text == "Gamma 0");
        Assert.Contains(commands, command => command.ID == "item_a");
        Assert.Contains(commands, command => command.ID == "group_g1_c");
    }

    [Fact]
    public void ClickAndHotkeyCallbacksInvokeCompanion()
    {
        var controller = new UIController();
        var ui = new UIBuilder<PixelTexture>(400, 400, new StubTextEngine(), controller);
        var view = new ControlFlowView();
        var context = new FlowContext
        {
            Items = [new FlowItem { Id = "a", Name = "Alpha" }],
        };

        view.Render(ui, context, null, null, null);
        ui.Build(0d);
        ElementBounds item = ui.GetElementBounds("item_a");
        int x = item.Rect.Left + 2;
        int y = item.Rect.Top + 2;

        controller.UpdateMouse(x, y, UIController.MouseButtons.None);
        view.Render(ui, context, null, null, null);
        ui.Build(0d);
        controller.UpdateMouse(x, y, UIController.MouseButtons.Left);
        view.Render(ui, context, null, null, null);
        ui.Build(0d);
        controller.UpdateMouse(x, y, UIController.MouseButtons.None);
        view.Render(ui, context, null, null, null);
        ui.Build(0d);

        Assert.Equal("a", view.SelectedId);
        Assert.Equal(0, view.SelectedIndex);

        controller.UpdateInputBuffer([new UIController.Input(UIController.Key.Esc, pressed: true)]);
        view.Render(ui, context, null, null, null);
        ui.Build(0d);
        Assert.Equal(1, view.HotkeyCount);
    }
}
