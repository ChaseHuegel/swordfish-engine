using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Theme;
using WaywardBeyond.Data.Localization;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

internal sealed class NewSavePage : IMenuPage<MenuPage>
{
    private static readonly char[] _saveNameTrimChars = [' ', '\t', '.', '\n', '\r'];

    private const int MAX_CREATE_MESSAGES = 5;
    private const int CREATE_MESSAGE_ROW_HEIGHT = 24;
    private const float CREATE_MESSAGE_INTERVAL_SECONDS = 2f;
    private const float CREATE_MESSAGE_FADE_SECONDS = 0.3f;
    private const string CREATE_MESSAGE_TAG = "game_create";

    public MenuPage ID => MenuPage.NewSave;

    private readonly GameSaveService _gameSaveService;
    private readonly GameSaveManager _gameSaveManager;
    private readonly IInputService _inputService;
    private readonly SoundEffectService _soundEffectService;
    private readonly ILocalization _localization;
    private readonly NameGenerator _nameGenerator;
    private readonly IAssetDatabase<LocalizedTags> _localizedTagDatabase;
    private readonly Randomizer _randomizer = new();
    private readonly List<string> _createMessages = [];
    private readonly List<double> _createMessageTimes = [];

    private readonly Widgets.ActionButtonOptions _cancelButton;
    private readonly Widgets.ActionButtonOptions _initializeButton;

    private TextBoxState _saveNameTextBox;
    private TextBoxState _seedTextBox;
    private Task<GameSave?>? _createTask;
    private string? _createError;
    private double _createTime;
    private double _nextCreateMessageTime;
    private string _lastCreateMessage = string.Empty;

    public NewSavePage(
        in GameSaveService gameSaveService,
        in GameSaveManager gameSaveManager,
        in IInputService inputService,
        in SoundEffectService soundEffectService,
        in ILocalization localization,
        in NameGenerator nameGenerator,
        in IAssetDatabase<LocalizedTags> localizedTagDatabase
    ) {
        _gameSaveService = gameSaveService;
        _gameSaveManager = gameSaveManager;
        _inputService = inputService;
        _soundEffectService = soundEffectService;
        _localization = localization;
        _nameGenerator = nameGenerator;
        _localizedTagDatabase = localizedTagDatabase;

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
                DrawCreating(ui, delta);
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

            string currentName = _saveNameTextBox.Text.ToString().Trim(_saveNameTrimChars);
            bool nameMissing = string.IsNullOrWhiteSpace(currentName);
            bool duplicateName = !nameMissing && _gameSaveService.GetSaves().Any(save => save.Name == currentName);
            bool canInitialize = !nameMissing && !duplicateName;
            bool seedMissing = string.IsNullOrWhiteSpace(_seedTextBox.Text.ToString());

            string? footerMessage = _createError;
            var footerColor = UiTheme.Danger;
            if (footerMessage == null)
            {
                if (nameMissing)
                {
                    footerMessage = _localization.GetString("ui.notification.name.required");
                }
                else if (duplicateName)
                {
                    footerMessage = _localization.GetString("ui.notification.name.taken");
                }
                else if (seedMissing)
                {
                    footerMessage = _localization.GetString("ui.menu.randomSeedWarning");
                    footerColor = UiTheme.Warning;
                }
            }

            bool initializeClicked = ui.ActionButton(
                "Button_Initialize",
                _localization.GetString("ui.button.initialize")!,
                _initializeButton with { Rect = layout.PrimaryButton, Enabled = canInitialize }
            );

            ui.FooterStatus(
                layout.Footer,
                layout.ReturnButton,
                layout.PrimaryButton,
                footerMessage,
                footerColor
            );

            if (initializeClicked)
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
        using (ui.MenuPanel(rect, _localization.GetString("ui.menu.sectorInitialization")!))
        {
            int padding = 20;
            int x = padding;
            const int inputWidth = 300;
            int formOffset = Math.Min(60, Math.Max(0, (rect.Size.Y - 190) / 2 - 38));
            int inputY = 54 + formOffset;

            ui.Label(
                _localization.GetString("ui.menu.sectorInitHint")!,
                Widgets.Rect(x, 50, rect.Size.X - padding * 2, 18),
                UiMetrics.MenuSmallFont,
                UiTheme.TextMuted,
                Anchors.Left | Anchors.Center
            );

            ui.Label(
                _localization.GetString("ui.field.galaxyName")!,
                Widgets.Rect(x, 38 + formOffset, 160, 16),
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
                ui.TextBox(
                    "TextBox_SaveName",
                    ref _saveNameTextBox,
                    new FontOptions { Size = UiMetrics.MenuBodyFont },
                    _inputService,
                    out Widgets.Interactions nameInteractions
                );
                nameInteractions.WithTextInputAudio(_soundEffectService);
                if (nameInteractions.Has(Widgets.Interactions.Input))
                {
                    _createError = null;
                }
            }

            if (ui.IconButton(
                    "Button_RandomSaveName",
                    Widgets.Rect(x + inputWidth + 8, inputY, UiMetrics.SmallIconButtonSize, UiMetrics.SmallIconButtonSize),
                    MenuGlyphs.Dice,
                    new Widgets.IconButtonOptions(SoundEffectService: _soundEffectService)
                ))
            {
                _createError = null;
                RandomizeName();
            }

            int seedY = 100 + formOffset;
            ui.Label(
                _localization.GetString("ui.field.worldSeed")!,
                Widgets.Rect(x, 96 + formOffset, 160, 16),
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
                ui.TextBox(
                    "TextBox_SaveSeed",
                    ref _seedTextBox,
                    new FontOptions { Size = UiMetrics.MenuBodyFont },
                    _inputService,
                    out Widgets.Interactions seedInteractions
                );
                seedInteractions.WithTextInputAudio(_soundEffectService);
                if (seedInteractions.Has(Widgets.Interactions.Input))
                {
                    _createError = null;
                }
            }

            if (ui.IconButton(
                    "Button_RandomSeed",
                    Widgets.Rect(x + inputWidth + 8, seedY + 12, UiMetrics.SmallIconButtonSize, UiMetrics.SmallIconButtonSize),
                    MenuGlyphs.Dice,
                    new Widgets.IconButtonOptions(SoundEffectService: _soundEffectService)
                ))
            {
                _createError = null;
                _seedTextBox.Text.Clear();
                _seedTextBox.Text.Append(Random.Shared.NextInt64());
            }
        }
    }

    private void DrawDossier(UIBuilder<Material> ui, IntRect rect)
    {
        using (ui.MenuPanel(rect, _localization.GetString("ui.menu.sectorDossier")!))
        {
            int padding = 20;
            string name = _saveNameTextBox.Text.ToString().Trim(_saveNameTrimChars);
            if (string.IsNullOrWhiteSpace(name))
            {
                name = _localization.GetString("ui.field.untitledSector")!;
            }

            ui.Label(
                name,
                Widgets.Rect(padding, 42, rect.Size.X - padding * 2, 26),
                UiMetrics.MenuTitleFont,
                UiTheme.Text,
                Anchors.Left | Anchors.Center
            );
            ui.Label(
                $"{_localization.GetString("ui.menu.mode")!}: CREATIVE",
                Widgets.Rect(padding, 76, rect.Size.X - padding * 2, 18),
                UiMetrics.MenuSmallFont,
                UiTheme.TextMuted,
                Anchors.Left | Anchors.Center
            );
            ui.Label(
                $"{_localization.GetString("ui.menu.seed")!}: {(_seedTextBox.Text.Length > 0 ? _seedTextBox.Text.ToString() : _localization.GetString("ui.field.random"))}",
                Widgets.Rect(padding, 96, rect.Size.X - padding * 2, 18),
                UiMetrics.MenuSmallFont,
                UiTheme.TextMuted,
                Anchors.Left | Anchors.Center
            );
            ui.Label(
                $"{_localization.GetString("ui.level.type")!}: {_localization.GetString("ui.level.type.asteroids")!}",
                Widgets.Rect(padding, 116, rect.Size.X - padding * 2, 18),
                UiMetrics.MenuSmallFont,
                UiTheme.TextMuted,
                Anchors.Left | Anchors.Center
            );

            ui.Divider(
                Widgets.Rect(padding, 150, rect.Size.X - padding * 2, 1),
                UiTheme.Border
            );
        }
    }

    private void DrawCreating(UIBuilder<Material> ui, double delta)
    {
        _createTime += delta;
        if (_createTime >= _nextCreateMessageTime)
        {
            AddCreateMessage();
        }

        MenuLayout layout = MenuLayout.Compute(
            ui.Width,
            ui.Height,
            maxPageWidth: 600,
            twoColumn: false
        );

        const int top = 48;
        int feedWidth = Math.Min(480, layout.PageWidth - 80);
        int feedX = (layout.PageWidth - feedWidth) / 2;
        int feedY = top + 40;
        int feedBottom = feedY + (MAX_CREATE_MESSAGES + 1) * CREATE_MESSAGE_ROW_HEIGHT;

        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
                Width = new Fixed(layout.PageWidth),
                Height = new Fixed(layout.PageHeight),
            };

            using (ui.MenuPanel(layout.Right, _localization.GetString("ui.menu.sectorInitialization")!))
            {
                ui.Label(
                    _localization.GetString("ui.menu.creatingSector")!,
                    Widgets.Rect(feedX, top, feedWidth, 24),
                    UiMetrics.MenuBodyFont,
                    UiTheme.Text,
                    Anchors.Left | Anchors.Center
                );

                for (var i = 0; i < _createMessages.Count; i++)
                {
                    float fade = Math.Clamp(
                        (float)((_createTime - _createMessageTimes[i]) / CREATE_MESSAGE_FADE_SECONDS),
                        0f,
                        1f
                    );
                    int lineY = feedY + i * CREATE_MESSAGE_ROW_HEIGHT;
                    ui.Label(
                        ">",
                        Widgets.Rect(feedX, lineY, 12, 18),
                        UiMetrics.MenuSmallFont,
                        Fade(UiTheme.Accent, fade),
                        Anchors.Left | Anchors.Center
                    );
                    ui.Label(
                        _createMessages[i],
                        Widgets.Rect(feedX + 14, lineY, feedWidth - 14, 18),
                        UiMetrics.MenuSmallFont,
                        Fade(UiTheme.TextMuted, fade),
                        Anchors.Left | Anchors.Center
                    );
                }

                int cursorY = feedY + _createMessages.Count * CREATE_MESSAGE_ROW_HEIGHT;
                ui.Label(
                    ">",
                    Widgets.Rect(feedX, cursorY, 12, 18),
                    UiMetrics.MenuSmallFont,
                    UiTheme.Accent,
                    Anchors.Left | Anchors.Center
                );
                if ((int)(_createTime * 2) % 2 == 0)
                {
                    using (ui.Element())
                    {
                        ui.Color = UiTheme.Accent;
                        ui.Constraints = Widgets.RectConstraints(
                            Widgets.Rect(feedX + 14, cursorY + 3, 8, 12)
                        );
                    }
                }

                ui.Divider(
                    Widgets.Rect(feedX, feedBottom, feedWidth, 1),
                    UiTheme.Border
                );
            }
        }
    }

    private void AddCreateMessage()
    {
        Result<LocalizedTags> localizedTags = _localizedTagDatabase.Get(CultureInfo.CurrentCulture.TwoLetterISOLanguageName);
        IReadOnlyList<string>? tags = localizedTags.Success
            ? localizedTags.Value.GetValues(CREATE_MESSAGE_TAG)
            : null;

        if (tags != null && tags.Count > 0)
        {
            string message = SelectDifferent(tags, _lastCreateMessage);
            _createMessages.Add(message);
            _createMessageTimes.Add(_createTime);
            if (_createMessages.Count > MAX_CREATE_MESSAGES)
            {
                _createMessages.RemoveAt(0);
                _createMessageTimes.RemoveAt(0);
            }

            _lastCreateMessage = message;
        }

        _nextCreateMessageTime = _createTime + CREATE_MESSAGE_INTERVAL_SECONDS;
    }

    private string SelectDifferent(IReadOnlyList<string> values, string last)
    {
        if (values.Count == 0)
        {
            return string.Empty;
        }

        string selected = _randomizer.Select(values);
        if (values.Count == 1 || selected != last)
        {
            return selected;
        }

        int index = 0;
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] == selected)
            {
                index = i;
                break;
            }
        }

        return values[(index + 1) % values.Count];
    }

    private static Vector4 Fade(Vector4 color, float alpha)
    {
        return new Vector4(color.X, color.Y, color.Z, color.W * alpha);
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
        _createTime = 0;
        _nextCreateMessageTime = 0;
        _lastCreateMessage = string.Empty;
        _createMessages.Clear();
        _createMessageTimes.Clear();
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
