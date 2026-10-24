using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Settings;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Config;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

internal sealed class MainMenuSettingsPage(
    in ConfigManager configManager,
    in ControlSettings controlSettings,
    in WindowSettings windowSettings,
    in RenderSettings renderSettings,
    in VolumeSettings volumeSettings,
    in GameplayConfig gameplayConfig,
    in UISettings uiSettings,
    in SoundEffectService soundEffectService,
    in IWindowContext windowContext,
    in IInputService inputService,
    in ILocalization localization
) : SettingsPage<MenuPage>(
    in configManager,
    in controlSettings,
    in windowSettings,
    in renderSettings,
    in volumeSettings,
    in gameplayConfig,
    in uiSettings,
    in soundEffectService,
    in windowContext,
    in inputService,
    in localization
) {
    /// <inheritdoc/>
    public override MenuPage ID => MenuPage.Settings;
}
