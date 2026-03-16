using Swordfish.Library.Globalization;
using Swordfish.Settings;
using WaywardBeyond.Client.Core.Configuration;
using WaywardBeyond.Client.Core.Services;

namespace WaywardBeyond.Client.Core.UI.Layers.Menus.Main;

internal sealed class MainMenuSettingsPage(
    in SettingsManager settingsManager,
    in ControlSettings controlSettings,
    in WindowSettings windowSettings,
    in RenderSettings renderSettings,
    in VolumeSettings volumeSettings,
    in SoundEffectService soundEffectService,
    in ILocalization localization
) : SettingsPage<MenuPage>(
    in settingsManager,
    in controlSettings,
    in windowSettings,
    in renderSettings,
    in volumeSettings,
    in soundEffectService,
    in localization
) {
    public override MenuPage ID => MenuPage.Settings;
}