using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Audio;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Configuration;
using WaywardBeyond.Client.Core.Saves;

namespace WaywardBeyond.Client.Core.UI.Layers.Menus.Pause;

internal sealed class HomePage(
    in IAudioService audioService,
    in VolumeSettings volumeSettings,
    in ILocalization localization,
    in GameSaveManager gameSaveManager
) : IMenuPage<PausePage>
{
    public PausePage ID => PausePage.Home;

    private readonly IAudioService _audioService = audioService;
    private readonly VolumeSettings _volumeSettings = volumeSettings;
    private readonly ILocalization _localization = localization;
    private readonly GameSaveManager _gameSaveManager = gameSaveManager;

    private readonly FontOptions _buttonFontOptions = new()
    {
        Size = 32,
    };

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<PausePage> menu)
    {
        if (ui.TextButton(id: "Button_Continue", text: _localization.GetString("ui.button.continue")!, _buttonFontOptions, _audioService, _volumeSettings))
        {
            WaywardBeyond.Unpause();
        }

        if (ui.TextButton(id: "Button_Settings", text: _localization.GetString("ui.button.settings")!, _buttonFontOptions, _audioService, _volumeSettings))
        {
            menu.GoToPage(PausePage.Settings);
        }
        
        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                Width = new Fill(),
                Height = new Fill(),
            };
        }

        if (ui.TextButton(id: "Button_SaveAndExit", text: _localization.GetString("ui.button.saveAndExit")!, _buttonFontOptions, _audioService, _volumeSettings))
        {
            _gameSaveManager.SaveAndExit();
        }
        
        return Result.FromSuccess();
    }
}