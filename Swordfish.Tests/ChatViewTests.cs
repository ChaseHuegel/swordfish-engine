using System.Collections.Generic;
using System.Globalization;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using WaywardBeyond.Client.UI.Layers;
using Xunit;

namespace Swordfish.Tests;

public class ChatViewTests
{
    private static readonly int[] Widths = [800, 1280, 1920];
    private static readonly int[] Heights = [600, 720, 1080];

    [Fact]
    public void ClosedHistoryMatchesSnapshot()
    {
        for (var i = 0; i < Widths.Length; i++)
        {
            int width = Widths[i];
            int height = Heights[i];
            UiSnapshotTests.Compare($"chat-closed-history-{width}x{height}", width, height, ui =>
            {
                new ChatView(new ChatLocalization()) { Input = new FakeInputService() }.Render(
                    ui,
                    new ChatViewState(
                        Open: false,
                        Messages:
                        [
                            new ChatMessageRowState("Ada", "Hello there.", 22, 1f, true),
                            new ChatMessageRowState("Bo", "General Kenobi!", 22, 0.5f, true),
                        ],
                        ShowThumb: false,
                        ThumbRatio: 0f,
                        ThumbOffset: 0f,
                        ViewportOffsetY: 153
                    )
                );
            });
        }
    }

    [Fact]
    public void OpenEmptyMatchesSnapshot()
    {
        for (var i = 0; i < Widths.Length; i++)
        {
            int width = Widths[i];
            int height = Heights[i];
            UiSnapshotTests.Compare($"chat-open-empty-{width}x{height}", width, height, ui =>
            {
                new ChatView(new ChatLocalization()) { Input = new FakeInputService() }.Render(
                    ui,
                    new ChatViewState(true, [], false, 0f, 0f, 200)
                );
            });
        }
    }

    [Fact]
    public void OpenWithMessagesMatchesSnapshot()
    {
        for (var i = 0; i < Widths.Length; i++)
        {
            int width = Widths[i];
            int height = Heights[i];
            UiSnapshotTests.Compare($"chat-open-messages-{width}x{height}", width, height, ui =>
            {
                var rows = new List<ChatMessageRowState>();
                for (var message = 0; message < 10; message++)
                {
                    rows.Add(new ChatMessageRowState("Ada", $"Message {message}.", 22, 1f, false));
                }

                new ChatView(new ChatLocalization()) { Input = new FakeInputService() }.Render(
                    ui,
                    new ChatViewState(true, rows, true, 0.905f, 0.095f, -20)
                );
            });
        }
    }

    [Fact]
    public void ClosedHistoryDoesNotCaptureTheCursor()
    {
        var controller = new UIController();
        var ui = new UIBuilder<Material>(800, 600, new StubTextEngine(), controller);
        var view = new ChatView(new ChatLocalization()) { Input = new FakeInputService() };
        var state = new ChatViewState(
            false,
            [new ChatMessageRowState("Ada", "Hello there.", 22, 1f, true)],
            false,
            0f,
            0f,
            178
        );

        view.Render(ui, state);
        ui.Build(0d);

        controller.UpdateMouse(30, 300, UIController.MouseButtons.None);
        view.Render(ui, state);
        ui.Build(0.008d);
        Assert.False(controller.IsMouseCaptured, "closed root captured the cursor");

        controller.UpdateMouse(580, 490, UIController.MouseButtons.None);
        view.Render(ui, state);
        ui.Build(0.010d);
        Assert.False(controller.IsMouseCaptured, "closed viewport captured the cursor");

        controller.UpdateMouse(580, 510, UIController.MouseButtons.None);
        view.Render(ui, state);
        ui.Build(0.012d);
        Assert.False(controller.IsMouseCaptured, "closed row captured the cursor");

        IntRect bounds = ui.GetElementBounds("ChatMessage_0").Rect;
        controller.UpdateMouse(bounds.Left + 2, bounds.Top + 2, UIController.MouseButtons.None);
        view.Render(ui, state);
        ui.Build(0.016d);

        Assert.False(controller.IsMouseCaptured, "closed message text captured the cursor");
    }

    [Fact]
    public void OpenChatKeepsFocusAndReportsSubmit()
    {
        var controller = new UIController();
        var ui = new UIBuilder<Material>(800, 600, new StubTextEngine(), controller);
        var view = new ChatView(new ChatLocalization()) { Input = new FakeInputService() };
        var state = new ChatViewState(true, [], false, 0f, 0f, 200);

        Frame(ui, view, state, 0d);
        Assert.True(controller.IsKeyboardCaptured);

        //  A click outside the box must not strand the send box unfocused.
        controller.UpdateMouse(1, 1, UIController.MouseButtons.Left);
        Frame(ui, view, state, 0.016d);
        Assert.True(controller.IsKeyboardCaptured);

        controller.UpdateInputBuffer([new UIController.Input('h'), new UIController.Input('i')]);
        Frame(ui, view, state, 0.032d);
        Assert.Equal("hi", view.ChatBox.Text.ToString());

        controller.UpdateInputBuffer([new UIController.Input(UIController.Key.Enter, true)]);
        Frame(ui, view, state, 0.048d);
        Assert.True(view.TakeSubmit());
    }

    private static void Frame(
        UIBuilder<Material> ui,
        ChatView view,
        ChatViewState state,
        double delta
    )
    {
        view.Render(ui, state);
        ui.Build(delta);
    }

    private sealed class ChatLocalization : ILocalization
    {
        public string? GetString(string value)
        {
            return value switch
            {
                "ui.label.chat.header" => "SUBSPACE COMMS // LOCAL",
                "ui.hint.chat.close" => "[ESC] CLOSE",
                "ui.hint.chat.send" => "[ENTER]",
                "ui.field.chat.input" => "Enter a message",
                _ => value,
            };
        }

        public string? GetString(string value, string cultureName) => GetString(value);

        public string? GetString(string value, CultureInfo cultureInfo) => GetString(value);
    }
}
