using Swordfish.Audio;
using Swordfish.Library.Globalization;
using Swordfish.Settings;
using WaywardBeyond.Client.Core.Configuration;

namespace WaywardBeyond.Client.Core.UI.Layers.Menus.Pause;

internal sealed class PauseMenuSettingsPage(
    in SettingsManager settingsManager,
    in ControlSettings controlSettings,
    in WindowSettings windowSettings,
    in RenderSettings renderSettings,
    in VolumeSettings volumeSettings,
    in IAudioService audioService,
    in ILocalization localization
) : SettingsPage<PausePage>(
    in settingsManager,
    in controlSettings,
    in windowSettings,
    in renderSettings,
    in volumeSettings,
    in audioService,
    in localization
) {
    public override PausePage ID => PausePage.Settings;
}