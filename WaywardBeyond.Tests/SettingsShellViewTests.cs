using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Settings;
using Swordfish.Tests;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.UI.Layers.Menus;
using WaywardBeyond.Client.UI.Layers.Menus.Main;
using WaywardBeyond.Client.UI.Settings;
using WaywardBeyond.Config;
using Xunit;

namespace WaywardBeyond.Tests;

public class SettingsShellViewTests
{
    [Fact]
    public void SettingsShellMatchesSnapshots()
    {
        Snapshot("settings-shell-wide", 1920, 1080);
        Snapshot("settings-shell-medium", 1280, 720);
        Snapshot("settings-shell-tabs", 800, 600);
    }

    private static void Snapshot(string name, int width, int height)
    {
        UiSnapshotTests.Compare(name, width, height, ui =>
        {
            var renderSettings = new RenderSettings();
            SettingsDefaults.ApplyRenderDefaults(renderSettings);

            var page = new SettingsPage<MenuPage>(
                MenuPage.Settings,
                configManager: null!,
                controlSettings: new ControlSettings(),
                keybindingSettings: new KeybindingSettings(),
                windowSettings: new WindowSettings(),
                renderSettings: renderSettings,
                volumeSettings: new VolumeSettings(),
                playbackDeviceService: new FakePlaybackDeviceService(),
                gameplayConfig: new GameplayConfig(),
                uiSettings: new UISettings(),
                soundEffectService: null!,
                windowContext: new FakeWindowContext(),
                shortcutService: new StubShortcutService(),
                localization: new TestLocalization()
            );

            page.OnEnter();
            page.RenderPage(delta: 0d, ui, menu: null!);
        });
    }
}
