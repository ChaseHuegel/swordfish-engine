using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Settings;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.UI.Layers.Menus;
using WaywardBeyond.Client.UI.Layers.Menus.Main;
using WaywardBeyond.Config;
using Xunit;

namespace Swordfish.Tests;

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
            var page = new SettingsPage<MenuPage>(
                MenuPage.Settings,
                configManager: null!,
                controlSettings: new ControlSettings(),
                windowSettings: new WindowSettings(),
                renderSettings: new RenderSettings(),
                volumeSettings: new VolumeSettings(),
                gameplayConfig: new GameplayConfig(),
                uiSettings: new UISettings(),
                soundEffectService: null!,
                windowContext: new FakeWindowContext(),
                inputService: new FakeInputService(),
                localization: new TestLocalization()
            );

            page.OnEnter();
            page.RenderPage(delta: 0d, ui, menu: null!);
        });
    }
}
