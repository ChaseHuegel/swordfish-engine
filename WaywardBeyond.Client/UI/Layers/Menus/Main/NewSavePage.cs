using System;
using System.Linq;
using System.Threading.Tasks;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

internal sealed class NewSavePage : IMenuPage<MenuPage>
{
    private static readonly char[] _saveNameTrimChars = [' ', '\t', '.', '\n', '\r'];

    public MenuPage ID => MenuPage.NewSave;

    private readonly GameSaveService _gameSaveService;
    private readonly IInputService _inputService;
    private readonly SoundEffectService _soundEffectService;
    private readonly ILocalization _localization;
    private readonly NameGenerator _nameGenerator;

    private readonly Widgets.ActionButtonOptions _primaryButton;
    private readonly Widgets.ActionButtonOptions _subtleButton;
    private readonly Widgets.ActionButtonOptions _smallIconButton;
    private readonly FontOptions _bodyFont;

    private TextBoxState _saveNameTextBox;
    private TextBoxState _seedTextBox;

    public NewSavePage(
        in GameSaveService gameSaveService,
        in IInputService inputService,
        in SoundEffectService soundEffectService,
        in ILocalization localization,
        in NameGenerator nameGenerator
    ) {
        _gameSaveService = gameSaveService;
        _inputService = inputService;
        _soundEffectService = soundEffectService;
        _localization = localization;
        _nameGenerator = nameGenerator;

        _primaryButton = Widgets.ActionButtonOptions.Menu(ButtonVariant.Primary, soundEffectService) with
        {
            Anchors = Anchors.Center,
        };
        _subtleButton = Widgets.ActionButtonOptions.Menu(ButtonVariant.Subtle, soundEffectService) with
        {
            Anchors = Anchors.Center,
        };
        _smallIconButton = Widgets.ActionButtonOptions.Icon(
            ButtonVariant.Secondary,
            soundEffectService,
            size: UiMetrics.SmallIconButtonSize,
            fontSize: UiMetrics.IconFont,
            fontId: "Font Awesome 6 Free Solid"
        );
        _bodyFont = new FontOptions
        {
            Size = UiMetrics.BodyFont,
        };

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
        if (!_gameSaveService.CanCreateSave)
        {
            menu.GoToPage(MenuPage.Home);
            return Result.FromSuccess();
        }

        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };

            using (ui.Text(_localization.GetString("ui.menu.createSave")!))
            {
                ui.FontSize = UiMetrics.TitleFont;
                ui.Color = UiTheme.Text;
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

            using (ui.Element())
            {
                ui.Spacing = 8;

                ui.TextBox(id: "TextBox_SaveName", state: ref _saveNameTextBox, _bodyFont, _inputService, _soundEffectService);

                if (ui.ActionButton("Button_RandomSaveName", "\uf074", _smallIconButton))
                {
                    NameGenerator.Options nameGeneratorOptions = new NameGenerator.Options(
                        TitleChance: 0.7f,
                        FirstNameChance: 0.7f,
                        LastNameChance: 1.0f,
                        SubtitleChance: 0.9f,
                        NicknameChance: 0.7f
                    );

                    string generatedName = _nameGenerator.Generate(key: "save", nameGeneratorOptions);

                    var attempts = 0;
                    while (attempts < 10 && (generatedName.Length > _saveNameTextBox.Settings.MaxCharacters || generatedName == _saveNameTextBox.Text.ToString()))
                    {
                        generatedName = _nameGenerator.Generate(key: "save", nameGeneratorOptions);
                        attempts++;
                    }

                    _saveNameTextBox.Text.Clear();
                    _saveNameTextBox.Text.Append(generatedName);
                }
            }

            var validSaveName = true;
            string saveNameValue = _saveNameTextBox.Text.ToString().Trim(_saveNameTrimChars);
            if (string.IsNullOrWhiteSpace(saveNameValue))
            {
                using (ui.Text(_localization.GetString("ui.notification.name.required")!))
                {
                    ui.Color = UiTheme.Danger;
                }
                validSaveName = false;
            }
            else if (_gameSaveService.GetSaves().Any(save => save.Name == saveNameValue))
            {
                using (ui.Text(_localization.GetString("ui.notification.name.taken")!))
                {
                    ui.Color = UiTheme.Danger;
                }
                validSaveName = false;
            }

            using (ui.Element())
            {
                ui.Spacing = 8;

                ui.TextBox(id: "TextBox_SaveSeed", state: ref _seedTextBox, _bodyFont, _inputService, _soundEffectService);

                if (ui.ActionButton("Button_RandomSeed", "\uf074", _smallIconButton))
                {
                    _seedTextBox.Text.Clear();
                    _seedTextBox.Text.Append(Random.Shared.NextInt64());
                }
            }

            if (ui.ActionButton("Button_NewGame", _localization.GetString("ui.button.newGame")!, _primaryButton))
            {
                if (validSaveName)
                {
                    var seedValue = _seedTextBox.Text.ToString();
                    string seed = string.IsNullOrWhiteSpace(seedValue) ? Random.Shared.NextInt64().ToString() : seedValue;
                    var options = new GameOptions(saveNameValue, seed);
                    Task.Run(() => _gameSaveService.CreateSave(options));
                    menu.GoToPage(MenuPage.SelectSave);
                    ResetState();
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

            if (ui.ActionButton("Button_Back", _localization.GetString("ui.button.back")!, _subtleButton))
            {
                menu.GoBack();
            }
        }

        return Result.FromSuccess();
    }

    private void ResetState()
    {
        _saveNameTextBox.Text.Clear();
        _seedTextBox.Text.Clear();
    }
}
