using System.Collections.Generic;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using WaywardBeyond.Client.Events;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Client.UI.Layers;
using WaywardBeyond.Config;
using Xunit;

namespace Swordfish.Tests;

[Collection("ClientGameState")]
public class ChatLayerTests
{
    [Fact]
    public void TypedSubmitSendsAndClosesTheBox()
    {
        WaywardBeyond.Client.WaywardBeyond.GameState.Set(WaywardBeyond.Client.GameState.Playing);
        try
        {
            var shortcuts = new CapturingShortcutService();
            var input = new FakeInputService();
            var config = new ChatConfig();
            var chat = new ChatService(config);
            var motion = new ClientPlayerMotionProcessor(
                input,
                new FakeWindowContext(),
                new FakePhysics(),
                new EventInvoker<PlayerMovedEvent>([])
            );
            var layer = new ChatLayer(
                shortcuts,
                input,
                new InteractionState(motion),
                chat,
                config,
                new TestLocalization()
            );

            var controller = new UIController();
            var ui = new UIBuilder<Material>(800, 600, new StubTextEngine(), controller);

            Shortcut open = shortcuts.Find("Open Chat");
            Assert.True(open.IsEnabled());
            open.Action();

            //  The opening Enter arrives in the same input stream and must not submit.
            controller.UpdateInputBuffer([new UIController.Input(UIController.Key.Enter, true)]);
            layer.RenderUI(0d, ui);
            ui.Build(0d);
            Assert.False(chat.TryDequeueSend(out _));

            controller.UpdateInputBuffer([new UIController.Input('h'), new UIController.Input('i')]);
            layer.RenderUI(0.016d, ui);
            ui.Build(0.016d);

            controller.UpdateInputBuffer([new UIController.Input(UIController.Key.Enter, true)]);
            layer.RenderUI(0.032d, ui);
            ui.Build(0.032d);

            Assert.True(chat.TryDequeueSend(out string value));
            Assert.Equal("hi", value);

            controller.UpdateInputBuffer([]);
            layer.RenderUI(0.048d, ui);
            Assert.Empty(ui.Build(0.048d));
        }
        finally
        {
            WaywardBeyond.Client.WaywardBeyond.GameState.Set(WaywardBeyond.Client.GameState.MainMenu);
        }
    }

    private sealed class CapturingShortcutService : IShortcutService
    {
        private readonly List<Shortcut> _shortcuts = [];

        public bool RegisterShortcut(Shortcut shortcut)
        {
            _shortcuts.Add(shortcut);
            return true;
        }

        public Shortcut Find(string name)
        {
            for (var i = 0; i < _shortcuts.Count; i++)
            {
                if (_shortcuts[i].Name == name)
                {
                    return _shortcuts[i];
                }
            }

            throw new Xunit.Sdk.XunitException($"No shortcut registered with name \"{name}\".");
        }
    }
}
