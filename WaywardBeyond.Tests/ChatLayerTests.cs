#nullable enable
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Input;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Tests;
using WaywardBeyond.Client.Events;
using WaywardBeyond.Client.Shortcuts;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Client.UI.Layers;
using WaywardBeyond.Config;
using Xunit;

namespace WaywardBeyond.Tests;

[Collection("ClientGameState")]
public class ChatLayerTests
{
    [Fact]
    public void EnterOpensTypingAndSameFrameEnterSendsAndCloses()
    {
        using var harness = new Harness();

        //  Opening with Enter (even with UI Enter buffered on the same frame) must not immediately submit.
        harness.PressShortcutKey(Key.Enter);
        harness.Render([new UIController.Input(UIController.Key.Enter, true)]);
        Assert.False(harness.Chat.TryDequeueSend(out _));
        Assert.True(harness.HasText("[ENTER]"));
        Assert.True(harness.HasText("[ESC] CLOSE"));

        harness.Render([new UIController.Input('h'), new UIController.Input('i')]);
        Assert.False(harness.Chat.TryDequeueSend(out _));

        //  Pressing Send Chat in the same frame as a final character captures the full line before closing.
        harness.PressShortcutKey(Key.Enter);
        harness.Render([new UIController.Input('!'), new UIController.Input(UIController.Key.Enter, true)]);

        Assert.True(harness.Chat.TryDequeueSend(out string value));
        Assert.Equal("hi!", value);

        harness.Render([]);
        Assert.Empty(harness.LastCommands);
    }

    [Fact]
    public void EmptySendClosesChatWithoutSending()
    {
        using var harness = new Harness();

        harness.PressShortcutKey(Key.Enter);
        harness.Render([]);
        Assert.True(harness.HasText("[ENTER]"));

        harness.PressShortcutKey(Key.Enter);
        harness.Render([]);

        Assert.False(harness.Chat.TryDequeueSend(out _));
        harness.Render([]);
        Assert.Empty(harness.LastCommands);
    }

    [Fact]
    public void ReboundSendChatUpdatesHintAndTriggersSend()
    {
        using var harness = new Harness();
        Assert.True(harness.Shortcuts.Rebind(GameShortcuts.SEND_CHAT, new ShortcutBinding(Key.Enter, ShortcutModifiers.Control)).Success);

        harness.PressShortcutKey(Key.Enter);
        harness.Render([]);
        Assert.True(harness.HasText("[CTRL+ENTER]"));
        Assert.False(harness.HasText("[ENTER]"));

        harness.Render([new UIController.Input('m'), new UIController.Input('s'), new UIController.Input('g')]);

        //  Plain Enter no longer sends when Send Chat requires Control+Enter.
        harness.PressShortcutKey(Key.Enter);
        harness.Render([new UIController.Input(UIController.Key.Enter, true)]);
        Assert.False(harness.Chat.TryDequeueSend(out _));
        Assert.True(harness.HasText("[CTRL+ENTER]"));

        harness.HoldKey(Key.Control);
        harness.PressShortcutKey(Key.Enter);
        harness.ReleaseKey(Key.Control);
        harness.Render([]);

        Assert.True(harness.Chat.TryDequeueSend(out string value));
        Assert.Equal("msg", value);

        harness.Render([]);
        Assert.Empty(harness.LastCommands);
    }

    [Fact]
    public void UnboundSendChatClearsHintAndStopsEnterSubmit()
    {
        using var harness = new Harness();
        Assert.True(harness.Shortcuts.Rebind(GameShortcuts.SEND_CHAT, ShortcutBinding.Unbound).Success);

        harness.PressShortcutKey(Key.Enter);
        harness.Render([]);
        Assert.False(harness.HasText("[ENTER]"));

        harness.Render([new UIController.Input('a'), new UIController.Input('b'), new UIController.Input('c')]);
        harness.PressShortcutKey(Key.Enter);
        harness.Render([new UIController.Input(UIController.Key.Enter, true)]);

        Assert.False(harness.Chat.TryDequeueSend(out _));
        Assert.NotEmpty(harness.LastCommands);

        harness.PressShortcutKey(Key.Esc);
        harness.Render([]);
        Assert.Empty(harness.LastCommands);
        Assert.False(harness.Chat.TryDequeueSend(out _));
    }

    [Fact]
    public void ReboundAndUnboundBackShortcutUpdatesChatCloseHintAndBehavior()
    {
        using var harness = new Harness();

        harness.PressShortcutKey(Key.Enter);
        harness.Render([]);
        Assert.True(harness.HasText("[ESC] CLOSE"));

        Assert.True(harness.Shortcuts.Rebind(GameShortcuts.BACK, new ShortcutBinding(Key.Backspace)).Success);
        harness.Render([]);
        Assert.True(harness.HasText("[BACKSPACE] CLOSE"));
        Assert.False(harness.HasText("[ESC] CLOSE"));

        //  Old Esc key no longer closes chat once Back / close moves to Backspace.
        harness.PressShortcutKey(Key.Esc);
        harness.Render([]);
        Assert.NotEmpty(harness.LastCommands);

        harness.PressShortcutKey(Key.Backspace);
        harness.Render([]);
        Assert.Empty(harness.LastCommands);

        //  Unbinding Back / close hides the close hint and ignores Esc.
        Assert.True(harness.Shortcuts.Rebind(GameShortcuts.BACK, ShortcutBinding.Unbound).Success);
        harness.PressShortcutKey(Key.Enter);
        harness.Render([]);
        Assert.False(harness.HasText("[ESC] CLOSE"));
        Assert.False(harness.HasText("[BACKSPACE] CLOSE"));

        harness.PressShortcutKey(Key.Esc);
        harness.Render([]);
        Assert.NotEmpty(harness.LastCommands);
    }

    private sealed class Harness : System.IDisposable
    {
        private readonly FakeInputService _input = new();
        private readonly UIController _controller = new();
        private readonly UIBuilder<Material> _ui;
        private readonly ChatLayer _layer;
        private double _time;

        public ShortcutService Shortcuts { get; }
        public ChatService Chat { get; }
        public List<RenderCommand<Material>> LastCommands { get; private set; } = [];

        public Harness()
        {
            WaywardBeyond.Client.WaywardBeyond.GameState.Set(WaywardBeyond.Client.GameState.Playing);
            Shortcuts = new ShortcutService(_input, NullLogger.Instance);
            var config = new ChatConfig();
            Chat = new ChatService(config);
            var motion = new ClientPlayerMotionProcessor(
                _input,
                new FakeWindowContext(),
                new FakePhysics(),
                new EventInvoker<PlayerMovedEvent>([])
            );
            _layer = new ChatLayer(
                Shortcuts,
                _input,
                new InteractionState(motion),
                Chat,
                config,
                new ChatTestLocalization()
            );
            _ui = new UIBuilder<Material>(800, 600, new StubTextEngine(), _controller);
        }

        public void Render(UIController.Input[] inputs)
        {
            _time += 0.016d;
            _controller.UpdateInputBuffer(inputs);
            _layer.RenderUI(0.016d, _ui);
            LastCommands = _ui.Build(_time);
        }

        public void HoldKey(Key key) => _input.HeldKeys.Add(key);

        public void ReleaseKey(Key key)
        {
            _input.HeldKeys.Remove(key);
            _input.KeyReleased?.Invoke(_input, new KeyEventArgs(key));
        }

        public void PressShortcutKey(Key key)
        {
            _input.HeldKeys.Add(key);
            _input.KeyPressed?.Invoke(_input, new KeyEventArgs(key));
            _input.HeldKeys.Remove(key);
            _input.KeyReleased?.Invoke(_input, new KeyEventArgs(key));
        }

        public bool HasText(string text)
        {
            for (var i = 0; i < LastCommands.Count; i++)
            {
                if (LastCommands[i].Text == text)
                {
                    return true;
                }
            }

            return false;
        }

        public void Dispose()
        {
            WaywardBeyond.Client.WaywardBeyond.GameState.Set(WaywardBeyond.Client.GameState.MainMenu);
        }
    }

    private sealed class ChatTestLocalization : ILocalization
    {
        public string? GetString(string value)
        {
            return value switch
            {
                "ui.label.chat.header" => "SUBSPACE COMMS // LOCAL",
                "ui.hint.chat.close" => "[{0}] CLOSE",
                "ui.hint.chat.send" => "[{0}]",
                "ui.field.chat.input" => "Enter a message",
                _ => value,
            };
        }

        public string? GetString(string value, string cultureName) => GetString(value);

        public string? GetString(string value, CultureInfo cultureInfo) => GetString(value);
    }
}
