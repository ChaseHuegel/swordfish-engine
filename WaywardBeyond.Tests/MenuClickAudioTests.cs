using System.Collections.Generic;
using System.Linq;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Util;
using Swordfish.Tests;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Client.UI.Layers.Menus.Main;
using WaywardBeyond.Data.Localization;
using Xunit;

namespace WaywardBeyond.Tests;

public class MenuClickAudioTests
{
    [Theory]
    [InlineData("Button_Multiplayer", (int)MenuPage.Multiplayer)]
    [InlineData("Button_Settings", (int)MenuPage.Settings)]
    [InlineData("Button_Credits", (int)MenuPage.Credits)]
    public void PageChangeButtonPlaysTheClickSound(string buttonID, int nextPage)
    {
        var soundEffectService = new RecordingSoundEffectService();
        var menu = new StubMenu(MenuPage.Home, MenuPage.Multiplayer, MenuPage.Settings, MenuPage.Credits);
        HomePage home = HomeFixtures.CreateHome(withSession: false, soundEffectService);
        var harness = new Harness(home, menu);

        harness.Click(buttonID);

        Assert.Equal([RecordingSoundEffectService.EXIT_CLICK_TAG], soundEffectService.Played);
        Assert.Equal((MenuPage)nextPage, menu.GetCurrentPage());
    }

    private sealed class Harness
    {
        private readonly UIController _controller = new();
        private readonly UIBuilder<Material> _ui;
        private readonly HomePage _home;
        private readonly Menu<MenuPage> _menu;
        private List<RenderCommand<Material>> _last = [];
        private double _delta;

        public Harness(HomePage home, Menu<MenuPage> menu)
        {
            _home = home;
            _menu = menu;
            _ui = new UIBuilder<Material>(1280, 720, new StubTextEngine(), _controller);
            Frame();
        }

        public void Click(string id)
        {
            IntRect target = Find(id);
            int x = target.Left + target.Size.X / 2;
            int y = target.Top + target.Size.Y / 2;

            _controller.UpdateMouse(x, y, UIController.MouseButtons.None);
            Frame();
            _controller.UpdateMouse(x, y, UIController.MouseButtons.Left);
            Frame();
            _controller.UpdateMouse(x, y, UIController.MouseButtons.None);
            Frame();
        }

        private void Frame()
        {
            _delta += 0.05d;
            _home.RenderPage(_delta, _ui, _menu);
            _last = _ui.Build(_delta);
        }

        private IntRect Find(string id)
        {
            for (var i = 0; i < _last.Count; i++)
            {
                if (_last[i].ID == id)
                {
                    return _last[i].Rect;
                }
            }

            throw new Xunit.Sdk.XunitException($"No render command found for \"{id}\".");
        }
    }

    private sealed class StubMenu(params MenuPage[] pages)
        : Menu<MenuPage>(null!, null!, pages.Select(page => new StubPage(page)).ToArray())
    {
        /// <inheritdoc/>
        public override bool IsVisible() => true;
    }

    private sealed class StubPage(MenuPage id) : IMenuPage<MenuPage>
    {
        public MenuPage ID => id;

        /// <inheritdoc/>
        public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu) => Result.FromSuccess();
    }

    private sealed class RecordingSoundEffectService()
        : SoundEffectService(new AudioChannelSystem(new VolumeSettings()), new StubTagDatabase())
    {
        public const string EXIT_CLICK_TAG = "ui_exit";

        public List<string> Played { get; } = [];

        /// <inheritdoc/>
        public override void PlayInterface(string tag) => Played.Add(tag);
    }

    private sealed class StubTagDatabase : ILocalizedTagsDatabase
    {
        private readonly LocalizedTags _tags = new(new Dictionary<string, List<string>>());

        /// <inheritdoc/>
        public LocalizedTags? Invariant => _tags;

        /// <inheritdoc/>
        public Result<LocalizedTags> Get(string id) => Result<LocalizedTags>.FromSuccess(_tags);
    }
}
