using System.Linq;
using System.Threading.Tasks;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Audio;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Configuration;
using WaywardBeyond.Client.Core.Saves;

namespace WaywardBeyond.Client.Core.UI.Layers.Menus.Main;

internal sealed class NewSavePage : IMenuPage<MenuPage>
{
    private static readonly char[] _saveNameTrimChars = [' ', '\t', '.'];

    public MenuPage ID => MenuPage.NewSave;
    
    private readonly GameSaveManager _gameSaveManager;
    private readonly GameSaveService _gameSaveService;
    private readonly IInputService _inputService;
    private readonly IAudioService _audioService;
    private readonly VolumeSettings _volumeSettings;
    private readonly ILocalization _localization;
    private readonly NotificationService _notificationService;

    private readonly Widgets.ButtonOptions _menuButtonOptions;
    private readonly Widgets.ButtonOptions _buttonOptions;

    private TextBoxState _saveNameTextBox;
    private TextBoxState _seedTextBox;

    public NewSavePage(
        in GameSaveManager gameSaveManager,
        in GameSaveService gameSaveService,
        in IInputService inputService,
        in IAudioService audioService,
        in VolumeSettings volumeSettings,
        in ILocalization localization,
        in NotificationService notificationService
    ) {
        _gameSaveManager = gameSaveManager;
        _gameSaveService = gameSaveService;
        _inputService = inputService;
        _audioService = audioService;
        _volumeSettings = volumeSettings;
        _localization = localization;
        _notificationService = notificationService;

        _menuButtonOptions = new Widgets.ButtonOptions(
            new FontOptions {
                Size = 32,
            },
            new Widgets.AudioOptions(audioService, volumeSettings)
        );
        
        _buttonOptions = new Widgets.ButtonOptions(
            new FontOptions {
                Size = 20,
            },
            new Widgets.AudioOptions(audioService, volumeSettings)
        );

        var saveNameTextBoxOptions = new TextBoxState.Options(
            Placeholder: localization.GetString("ui.field.saveName"),
            MaxCharacters: 20,
            DisallowedCharacters: ['\0', '\\', '/', ':', '*', '?', '"', '<', '>', '|'],
            Constraints: new Constraints
            {
                Width = new Fixed(300),
            }
        );
        _saveNameTextBox = new TextBoxState(initialValue: string.Empty, options: saveNameTextBoxOptions);
        
        var saveSeedTextBoxOptions = new TextBoxState.Options(
            Placeholder: localization.GetString("ui.field.saveSeed"),
            MaxCharacters: 20,
            Constraints: new Constraints
            {
                Width = new Fixed(300),
            }
        );
        
        _seedTextBox = new TextBoxState(initialValue: string.Empty, saveSeedTextBoxOptions);
    }

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };
            
            using (ui.Text(_localization.GetString("ui.menu.createSave")!))
            {
                ui.FontSize = 24;
            }
        }
        
        using (ui.Element())
        {
            ui.Spacing = 8;
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };
            
            ui.TextBox(id: "TextBox_SaveName", state: ref _saveNameTextBox, _buttonOptions.FontOptions, _inputService, _audioService, _volumeSettings);
            ui.TextBox(id: "TextBox_SaveSeed", state: ref _seedTextBox, _buttonOptions.FontOptions, _inputService, _audioService, _volumeSettings);
            
            string saveNameValue = _saveNameTextBox.Text.ToString().Trim(_saveNameTrimChars);
            using (ui.TextButton(id: "Button_NewGame", text: _localization.GetString("ui.button.newGame")!, _buttonOptions, out Widgets.Interactions interactions))
            {
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                };
                
                if (interactions.Has(Widgets.Interactions.Click))
                {
                    if (string.IsNullOrWhiteSpace(saveNameValue))
                    {
                        var notification = new Notification(_localization.GetString("ui.notification.nameEmpty")!, NotificationType.Interaction);
                        _notificationService.Push(notification);
                    }
                    else if (_gameSaveService.GetSaves().Any(save => save.Name == saveNameValue))
                    {
                        var notification = new Notification(_localization.GetString("ui.notification.nameTaken")!, NotificationType.Interaction);
                        _notificationService.Push(notification);
                    }
                    else
                    {
                        var seedValue = _seedTextBox.Text.ToString();
                        string seed = string.IsNullOrWhiteSpace(seedValue) ? "wayward beyond" : seedValue;
                        var options = new GameOptions(saveNameValue, seed);
                        Task.Run(() => _gameSaveManager.NewGame(options));
                    }
                }
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
        
        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };
            
            using (ui.TextButton(id: "Button_Back", text: "Back", _menuButtonOptions, out Widgets.Interactions interactions))
            {
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center,
                };

                if (interactions.Has(Widgets.Interactions.Click))
                {
                    menu.GoBack();
                }
            }
        }
        
        return Result.FromSuccess();
    }
}