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

    private readonly ILocalization _localization = localization;
    private readonly GameSaveManager _gameSaveManager = gameSaveManager;
    
    private readonly Widgets.ButtonOptions _buttonOptions = new(
        new FontOptions {
            Size = 32,
        },
        new Widgets.AudioOptions(audioService, volumeSettings)
    );

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<PausePage> menu)
    {
        using (ui.TextButton(id: "Button_Continue", text: _localization.GetString("ui.button.continue")!, _buttonOptions, out Widgets.Interactions interactions))
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };

            if (interactions.Has(Widgets.Interactions.Click))
            {
                WaywardBeyond.Unpause();
            }
        }

        using (ui.TextButton(id: "Button_Settings", text: _localization.GetString("ui.button.settings")!, _buttonOptions, out Widgets.Interactions interactions))
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };

            if (interactions.Has(Widgets.Interactions.Click))
            {
                menu.GoToPage(PausePage.Settings);
            }
        }
        
        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                Width = new Fill(),
                Height = new Fill(),
            };
        }

        using (ui.TextButton(id: "Button_SaveAndExit", text: _localization.GetString("ui.button.saveAndExit")!, _buttonOptions, out Widgets.Interactions interactions))
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };

            if (interactions.Has(Widgets.Interactions.Click))
            {
                _gameSaveManager.SaveAndExit();
            }
        }
        
        return Result.FromSuccess();
    }
}