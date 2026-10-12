using Swordfish.Library.Globalization;
using Swordfish.Settings;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Config;

namespace WaywardBeyond.Client.UI.Layers.Menus.Pause;

internal sealed class PauseMenuSettingsPage(
    in ConfigManager configManager,
    in ControlSettings controlSettings,
    in WindowSettings windowSettings,
    in RenderSettings renderSettings,
    in VolumeSettings volumeSettings,
    in GameplayConfig gameplayConfig,
    in UISettings uiSettings,
    in SoundEffectService soundEffectService,
    in ILocalization localization
) : SettingsPage<PausePage>(
    in configManager,
    in controlSettings,
    in windowSettings,
    in renderSettings,
    in volumeSettings,
    in gameplayConfig,
    in uiSettings,
    in soundEffectService,
    in localization
) {
    public override PausePage ID => PausePage.Settings;
}