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
    private readonly GameSaveManager _gameSaveManager;
    private readonly IInputService _inputService;
    private readonly SoundEffectService _soundEffectService;
    private readonly ILocalization _localization;
    private readonly NameGenerator _nameGenerator;

    private readonly Widgets.ActionButtonOptions _cancelButton;
    private readonly Widgets.ActionButtonOptions _initializeButton;

    private TextBoxState _saveNameTextBox;
    private TextBoxState _seedTextBox;
    private Task<GameSave?>? _createTask;
    private string? _createError;

    public NewSavePage(
        in GameSaveService gameSaveService,
        in GameSaveManager gameSaveManager,
        in IInputService inputService,
        in SoundEffectService soundEffectService,
        in ILocalization localization,
        in NameGenerator nameGenerator
    ) {
        _gameSaveService = gameSaveService;
        _gameSaveManager = gameSaveManager;
        _inputService = inputService;
        _soundEffectService = soundEffectService;
        _localization = localization;
        _nameGenerator = nameGenerator;

        _cancelButton = Widgets.ActionButtonOptions.Menu(ButtonVariant.Secondary, soundEffectService);
        _initializeButton = Widgets.ActionButtonOptions.Menu(ButtonVariant.Primary, soundEffectService);
        var saveNameTextBoxOptions = new TextBoxState.Options(
            Placeholder: localization.GetString("ui.field.saveName"),
            MaxCharacters: 20,
            DisallowedCharacters: ['\0', '\\', '/', ':', '*', '?', '"', '<', '>', '|'],
            Constraints: new Constraints
            {
                Width = new Fixed(300),
                Height = new Fixed(UiMetrics.ControlHeight),
            }
        );
        _saveNameTextBox = new TextBoxState(initialValue: string.Empty, options: saveNameTextBoxOptions);

        var saveSeedTextBoxOptions = new TextBoxState.Options(
            Placeholder: localization.GetString("ui.field.saveSeed"),
            MaxCharacters: 20,
            Constraints: new Constraints
            {
                Width = new Fixed(300),
                Height = new Fixed(UiMetrics.ControlHeight),
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

        if (_createTask != null)
        {
            if (!_createTask.IsCompleted)
            {
                DrawCreating(ui);
                return Result.FromSuccess();
            }

            if (_createTask.IsFaulted)
            {
                _createTask = null;
                _createError = _localization.GetString("ui.menu.createFailed") ?? "Failed to create the sector.";
                return Result.FromSuccess();
            }

            GameSave? created = _createTask.Result;
            _createTask = null;
            if (created == null)
            {
                _createError = _localization.GetString("ui.menu.createFailed") ?? "Failed to create the sector.";
                return Result.FromSuccess();
            }

            _gameSaveManager.ActiveSave = created;
            _saveNameTextBox.Text.Clear();
            _seedTextBox.Text.Clear();
            menu.GoToPage(MenuPage.SelectCharacter);
            return Result.FromSuccess();
        }

        MenuLayout layout = MenuLayout.Compute(
            ui.Width,
            ui.Height,
            maxPageWidth: 1160,
            twoColumn: true,
            leftRatio: 0.5f
        );

        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
                Width = new Fixed(layout.PageWidth),
                Height = new Fixed(layout.PageHeight),
            };
            ui.ClipConstraints = new Constraints
            {
                Width = new Fixed(layout.PageWidth),
                Height = new Fixed(layout.PageHeight),
            };

            ui.Label(
                _localization.GetString("ui.menu.sectorSpecification")!,
                layout.Title,
                UiMetrics.MenuTitleFont,
                UiTheme.Text,
                Anchors.Center
            );

            if (layout.UseTabs)
            {
                DrawTabs(ui, layout);
                if (_narrowTab == 0)
                {
                    DrawInitialization(ui, layout.Right);
                }
                else
                {
                    DrawDossier(ui, layout.Right);
                }
            }
            else
            {
                DrawInitialization(ui, layout.Left);
                DrawDossier(ui, layout.Right);
            }

            if (ui.ActionButton(
                    "Button_Cancel",
                    _localization.GetString("ui.settings.cancel")!,
                    _cancelButton with { Rect = layout.ReturnButton }
                ))
            {
                menu.GoToPage(MenuPage.Home);
            }

            if (ui.ActionButton(
                    "Button_Initialize",
                    _localization.GetString("ui.button.initialize")!,
                    _initializeButton with { Rect = layout.PrimaryButton }
                ))
            {
                TryCreate();
            }
        }

        return Result.FromSuccess();
    }

    private int _narrowTab;

    private void DrawTabs(UIBuilder<Material> ui, in MenuLayout layout)
    {
        int tabWidth = layout.PageWidth / 2;
        if (ui.Tab(
                "InitializationTab",
                Widgets.Rect(0, layout.Tabs.Top, tabWidth - 4, layout.Tabs.Size.Y),
                _localization.GetString("ui.menu.sectorInitialization")!,
                selected: _narrowTab == 0
            ))
        {
            _narrowTab = 0;
        }

        if (ui.Tab(
                "DossierTab",
                Widgets.Rect(tabWidth + 4, layout.Tabs.Top, tabWidth - 4, layout.Tabs.Size.Y),
                _localization.GetString("ui.menu.sectorDossier")!,
                selected: _narrowTab == 1
            ))
        {
            _narrowTab = 1;
        }
    }

    private void DrawInitialization(UIBuilder<Material> ui, IntRect rect)
    {
        using (ui.BracketPanel(rect, _localization.GetString("ui.menu.sectorInitialization")!))
        {
            int padding = 20;
            int x = padding;
            const int inputWidth = 300;
            int inputY = 44;

            ui.Label(
                _localization.GetString("ui.field.galaxyName")!,
                Widgets.Rect(x, 30, 120, 18),
                UiMetrics.MenuSmallFont,
                UiTheme.Text,
                Anchors.Left | Anchors.Center
            );

            using (ui.Element())
            {
                ui.Constraints = Widgets.RectConstraints(Widgets.Rect(x, inputY, inputWidth, UiMetrics.ControlHeight));
                ui.ClipConstraints = new Constraints
                {
                    Width = new Fixed(inputWidth),
                    Height = new Fixed(UiMetrics.ControlHeight),
                };
                ui.TextBox("TextBox_SaveName", ref _saveNameTextBox, new FontOptions { Size = UiMetrics.MenuBodyFont }, _inputService, _soundEffectService);
            }

            if (ui.ActionButton(
                    "Button_RandomSaveName",
                    Widgets.Rect(x + inputWidth + 8, inputY, UiMetrics.SmallIconButtonSize, UiMetrics.SmallIconButtonSize),
                    MenuGlyphs.Dice,
                    ButtonVariant.Secondary,
                    fontId: UiTheme.FontIcons,
                    soundEffectService: _soundEffectService
                ))
            {
                RandomizeName();
            }

            int seedY = 92;
            ui.Label(
                _localization.GetString("ui.field.worldSeed")!,
                Widgets.Rect(x, 78, 120, 18),
                UiMetrics.MenuSmallFont,
                UiTheme.Text,
                Anchors.Left | Anchors.Center
            );

            using (ui.Element())
            {
                ui.Constraints = Widgets.RectConstraints(Widgets.Rect(x, seedY + 12, inputWidth, UiMetrics.ControlHeight));
                ui.ClipConstraints = new Constraints
                {
                    Width = new Fixed(inputWidth),
                    Height = new Fixed(UiMetrics.ControlHeight),
                };
                ui.TextBox("TextBox_SaveSeed", ref _seedTextBox, new FontOptions { Size = UiMetrics.MenuBodyFont }, _inputService, _soundEffectService);
            }

            if (ui.ActionButton(
                    "Button_RandomSeed",
                    Widgets.Rect(x + inputWidth + 8, seedY + 12, UiMetrics.SmallIconButtonSize, UiMetrics.SmallIconButtonSize),
                    MenuGlyphs.Dice,
                    ButtonVariant.Secondary,
                    fontId: UiTheme.FontIcons,
                    soundEffectService: _soundEffectService
                ))
            {
                _seedTextBox.Text.Clear();
                _seedTextBox.Text.Append(Random.Shared.NextInt64());
            }

            int errorY = 136;
            string saveName = _saveNameTextBox.Text.ToString().Trim(_saveNameTrimChars);
            string? error = _createError;
            if (error == null && string.IsNullOrWhiteSpace(saveName))
            {
                error = _localization.GetString("ui.notification.name.required");
            }
            else if (error == null && _gameSaveService.GetSaves().Any(save => save.Name == saveName))
            {
                error = _localization.GetString("ui.notification.name.taken");
            }

            if (error != null)
            {
                ui.Label(
                    error,
                    Widgets.Rect(x, errorY, rect.Size.X - padding * 2, 18),
                    UiMetrics.MenuSmallFont,
                    UiTheme.Danger,
                    Anchors.Left | Anchors.Center
                );
            }
        }
    }

    private void DrawDossier(UIBuilder<Material> ui, IntRect rect)
    {
        using (ui.BracketPanel(rect, _localization.GetString("ui.menu.sectorDossier")!))
        {
            int padding = 20;
            string name = _saveNameTextBox.Text.ToString().Trim(_saveNameTrimChars);
            if (string.IsNullOrWhiteSpace(name))
            {
                name = _localization.GetString("ui.field.untitledSector")!;
            }

            ui.Label(
                name,
                Widgets.Rect(padding, 36, rect.Size.X - padding * 2, 26),
                UiMetrics.MenuTitleFont,
                UiTheme.Text,
                Anchors.Left | Anchors.Center
            );
            ui.Label(
                $"{_localization.GetString("ui.menu.mode")!}: CREATIVE",
                Widgets.Rect(padding, 70, rect.Size.X - padding * 2, 18),
                UiMetrics.MenuSmallFont,
                UiTheme.TextMuted,
                Anchors.Left | Anchors.Center
            );
            ui.Label(
                $"{_localization.GetString("ui.menu.seed")!}: {(_seedTextBox.Text.Length > 0 ? _seedTextBox.Text.ToString() : _localization.GetString("ui.field.random"))}",
                Widgets.Rect(padding, 90, rect.Size.X - padding * 2, 18),
                UiMetrics.MenuSmallFont,
                UiTheme.TextMuted,
                Anchors.Left | Anchors.Center
            );
            ui.Label(
                $"{_localization.GetString("ui.menu.generation")!}: {_localization.GetString("ui.menu.procedural")!}",
                Widgets.Rect(padding, 110, rect.Size.X - padding * 2, 18),
                UiMetrics.MenuSmallFont,
                UiTheme.TextMuted,
                Anchors.Left | Anchors.Center
            );

            ui.InfoBox(
                Widgets.Rect(padding, 144, rect.Size.X - padding * 2, 126),
                _localization.GetString("ui.menu.sectorInfoTitle")!,
                _localization.GetString("ui.menu.sectorInfoBody")!,
                _localization.GetString("ui.menu.sectorInfoPrefix")
            );
        }
    }

    private void DrawCreating(UIBuilder<Material> ui)
    {
        MenuLayout layout = MenuLayout.Compute(
            ui.Width,
            ui.Height,
            maxPageWidth: 600,
            twoColumn: false
        );

        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
                Width = new Fixed(layout.PageWidth),
                Height = new Fixed(layout.PageHeight),
            };

            using (ui.BracketPanel(layout.Right, _localization.GetString("ui.menu.sectorInitialization")!))
            {
                ui.Label(
                    _localization.GetString("ui.menu.creatingSector")!,
                    Widgets.Rect(0, 48, layout.PageWidth, 24),
                    UiMetrics.MenuBodyFont,
                    UiTheme.Text,
                    Anchors.Center
                );
            }

        }
    }

    private void TryCreate()
    {
        string saveName = _saveNameTextBox.Text.ToString().Trim(_saveNameTrimChars);
        if (string.IsNullOrWhiteSpace(saveName) || _gameSaveService.GetSaves().Any(save => save.Name == saveName))
        {
            _createError = string.IsNullOrWhiteSpace(saveName)
                ? _localization.GetString("ui.notification.name.required") ?? "Enter a name."
                : _localization.GetString("ui.notification.name.taken") ?? "That name is already used.";
            return;
        }

        string seedValue = _seedTextBox.Text.ToString();
        string seed = string.IsNullOrWhiteSpace(seedValue) ? Random.Shared.NextInt64().ToString() : seedValue;
        _createError = null;
        _createTask = Task.Run(() => _gameSaveService.CreateSaveAsync(new GameOptions(saveName, seed)));
    }

    private void RandomizeName()
    {
        NameGenerator.Options options = new(
            TitleChance: 0.7f,
            FirstNameChance: 0.7f,
            LastNameChance: 1.0f,
            SubtitleChance: 0.9f,
            NicknameChance: 0.7f
        );

        string generatedName = _nameGenerator.Generate(key: "save", options);
        var attempts = 0;
        while (attempts < 10 && (generatedName.Length > _saveNameTextBox.Settings.MaxCharacters || generatedName == _saveNameTextBox.Text.ToString()))
        {
            generatedName = _nameGenerator.Generate(key: "save", options);
            attempts++;
        }

        _saveNameTextBox.Text.Clear();
        _saveNameTextBox.Text.Append(generatedName);
    }
}
