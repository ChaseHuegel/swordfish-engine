using Swordfish.Library.Globalization;
using Swordfish.Settings;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Config;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

internal sealed class MainMenuSettingsPage(
    in SettingsManager settingsManager,
    in ControlSettings controlSettings,
    in WindowSettings windowSettings,
    in RenderSettings renderSettings,
    in VolumeSettings volumeSettings,
    in GameplayConfig gameplayConfig,
    in UISettings uiSettings,
    in SoundEffectService soundEffectService,
    in ILocalization localization
) : SettingsPage<MenuPage>(
    in settingsManager,
    in controlSettings,
    in windowSettings,
    in renderSettings,
    in volumeSettings,
    in gameplayConfig,
    in uiSettings,
    in soundEffectService,
    in localization
) {
    public override MenuPage ID => MenuPage.Settings;
}