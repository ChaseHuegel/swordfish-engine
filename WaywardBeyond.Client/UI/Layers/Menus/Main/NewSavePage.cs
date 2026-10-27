using Reef.Constraints;
using Reef.UI;
using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System;
using WaywardBeyond.Client.Extensions;
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
    private const int INPUT_WIDTH = 300;

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
    private int _narrowTab;

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
                Height = new Fixed(UiMetrics.CONTROL_HEIGHT),
            }
        );
        _saveNameTextBox = new TextBoxState(initialValue: string.Empty, options: saveNameTextBoxOptions);

        var saveSeedTextBoxOptions = new TextBoxState.Options(
            Placeholder: localization.GetString("ui.field.saveSeed"),
            MaxCharacters: 20,
            Constraints: new Constraints
            {
                Width = new Fixed(300),
                Height = new Fixed(UiMetrics.CONTROL_HEIGHT),
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

        Task<GameSave?>? createTask = _createTask;
        if (createTask != null)
        {
            return RenderCreateTask(ui, menu, createTask, delta);
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

            DrawPanes(ui, layout);
            DrawFooter(ui, layout, menu);
        }

        return Result.FromSuccess();
    }

    /// <summary>Draws the creation progress, or completes the pending save creation.</summary>
    private Result RenderCreateTask(
        UIBuilder<Material> ui,
        Menu<MenuPage> menu,
        Task<GameSave?> createTask,
        double delta
    )
    {
        if (!createTask.IsCompleted)
        {
            DrawCreating(ui, delta);
            return Result.FromSuccess();
        }

        if (createTask.IsFaulted)
        {
            _createTask = null;
            _createError = _localization.GetString("ui.menu.createFailed") ?? "Failed to create the sector.";
            return Result.FromSuccess();
        }

        GameSave? created = createTask.Result;
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

    /// <summary>Draws the initialization and dossier panes for the current width.</summary>
    private void DrawPanes(UIBuilder<Material> ui, in MenuLayout layout)
    {
        if (!layout.UseTabs)
        {
            DrawInitialization(ui, layout.Left);
            DrawDossier(ui, layout.Right);
            return;
        }

        DrawTabs(ui, layout);
        if (_narrowTab == 0)
        {
            DrawInitialization(ui, layout.Right);
            return;
        }

        DrawDossier(ui, layout.Right);
    }

    /// <summary>Draws the narrow-layout tabs.</summary>
    private void DrawTabs(UIBuilder<Material> ui, in MenuLayout layout)
    {
        int tabWidth = layout.PageWidth / 2;

        IntRect initializationRect = Widgets.Rect(0, layout.Tabs.Top, tabWidth - 4, layout.Tabs.Size.Y);
        bool initializationSelected = _narrowTab == 0;
        string initializationLabel = _localization.GetRequired("ui.menu.sectorInitialization");
        if (ui.Tab("InitializationTab", initializationRect, initializationLabel, initializationSelected))
        {
            _narrowTab = 0;
        }

        IntRect dossierRect = Widgets.Rect(tabWidth + 4, layout.Tabs.Top, tabWidth - 4, layout.Tabs.Size.Y);
        bool dossierSelected = _narrowTab == 1;
        string dossierLabel = _localization.GetRequired("ui.menu.sectorDossier");
        if (ui.Tab("DossierTab", dossierRect, dossierLabel, dossierSelected))
        {
            _narrowTab = 1;
        }
    }

    /// <summary>Draws the page footer, then starts the save creation when asked.</summary>
    private void DrawFooter(UIBuilder<Material> ui, in MenuLayout layout, Menu<MenuPage> menu)
    {
        string currentName = _saveNameTextBox.Text.ToString().Trim(_saveNameTrimChars);
        bool nameMissing = string.IsNullOrWhiteSpace(currentName);
        bool duplicateName = !nameMissing && _gameSaveService.GetSaves().Any(save => save.Name == currentName);
        bool canInitialize = !nameMissing && !duplicateName;
        bool seedMissing = string.IsNullOrWhiteSpace(_seedTextBox.Text.ToString());
        FooterStatus status = ResolveFooterStatus(nameMissing, duplicateName, seedMissing);

        var cancel = new MenuLayout.MenuFooterButton(
            "Button_Cancel",
            _localization.GetRequired("ui.settings.cancel"),
            _cancelButton
        );
        var initialize = new MenuLayout.MenuFooterButton(
            "Button_Initialize",
            _localization.GetRequired("ui.button.initialize"),
            _initializeButton with { Enabled = canInitialize }
        );

        MenuLayout.MenuFooterResult footer = layout.DrawFooter(
            ui,
            status.Message,
            cancel,
            initialize,
            status.Color
        );
        if (footer.BackClicked)
        {
            menu.GoToPage(MenuPage.Home);
        }

        if (footer.PrimaryClicked)
        {
            TryCreate();
        }
    }

    /// <summary>Gets the footer message and its color for the current name and seed.</summary>
    private FooterStatus ResolveFooterStatus(bool nameMissing, bool duplicateName, bool seedMissing)
    {
        if (_createError != null)
        {
            return new FooterStatus(_createError, UiTheme.Danger);
        }

        if (nameMissing)
        {
            return new FooterStatus(_localization.GetString("ui.notification.name.required"), UiTheme.Danger);
        }

        if (duplicateName)
        {
            return new FooterStatus(_localization.GetString("ui.notification.name.taken"), UiTheme.Danger);
        }

        if (!seedMissing)
        {
            return default;
        }

        return new FooterStatus(
            _localization.GetString("ui.menu.randomSeedWarning"),
            UiTheme.Warning
        );
    }

    /// <summary>A footer message and the color to draw it in.</summary>
    private readonly record struct FooterStatus(string? Message, Vector4 Color);

    /// <summary>Draws the sector initialization panel.</summary>
    private void DrawInitialization(UIBuilder<Material> ui, IntRect rect)
    {
        using (ui.MenuPanel(rect, _localization.GetRequired("ui.menu.sectorInitialization")))
        {
            const int padding = 20;
            int formOffset = Math.Min(60, Math.Max(0, (rect.Size.Y - 190) / 2 - 38));
            int inputY = 54 + formOffset;
            int seedY = 100 + formOffset;

            ui.Label(
                _localization.GetRequired("ui.menu.sectorInitHint"),
                Widgets.Rect(padding, 50, rect.Size.X - padding * 2, 18),
                UiMetrics.MENU_SMALL_FONT,
                UiTheme.TextMuted,
                Anchors.Left | Anchors.Center
            );

            ui.Label(
                _localization.GetRequired("ui.field.galaxyName"),
                Widgets.Rect(padding, 38 + formOffset, 160, 16),
                UiMetrics.MENU_SMALL_FONT,
                UiTheme.Text,
                Anchors.Left | Anchors.Center
            );

            DrawSaveNameField(ui, padding, inputY);

            ui.Label(
                _localization.GetRequired("ui.field.worldSeed"),
                Widgets.Rect(padding, 96 + formOffset, 160, 16),
                UiMetrics.MENU_SMALL_FONT,
                UiTheme.Text,
                Anchors.Left | Anchors.Center
            );

            DrawSeedField(ui, padding, seedY);
        }
    }

    /// <summary>Draws the sector name field and its randomize button.</summary>
    private void DrawSaveNameField(UIBuilder<Material> ui, int x, int inputY)
    {
        IntRect textBoxRect = Widgets.Rect(x, inputY, INPUT_WIDTH, UiMetrics.CONTROL_HEIGHT);
        Widgets.Interactions nameInteractions = ui.MenuTextBox(
            "TextBox_SaveName",
            ref _saveNameTextBox,
            textBoxRect,
            UiMetrics.MENU_BODY_FONT,
            _inputService
        );
        nameInteractions.WithTextInputAudio(_soundEffectService);
        if (nameInteractions.Has(Widgets.Interactions.Input))
        {
            _createError = null;
        }

        IntRect randomRect = Widgets.Rect(
            x + INPUT_WIDTH + 8,
            inputY,
            UiMetrics.SMALL_ICON_BUTTON_SIZE,
            UiMetrics.SMALL_ICON_BUTTON_SIZE
        );
        Widgets.IconButtonOptions randomOptions = new(SoundEffectService: _soundEffectService);
        if (ui.IconButton("Button_RandomSaveName", randomRect, MenuGlyphs.DICE, randomOptions))
        {
            _createError = null;
            RandomizeName();
        }
    }

    /// <summary>Draws the world seed field and its randomize button.</summary>
    private void DrawSeedField(UIBuilder<Material> ui, int x, int seedY)
    {
        IntRect textBoxRect = Widgets.Rect(x, seedY + 12, INPUT_WIDTH, UiMetrics.CONTROL_HEIGHT);
        Widgets.Interactions seedInteractions = ui.MenuTextBox(
            "TextBox_SaveSeed",
            ref _seedTextBox,
            textBoxRect,
            UiMetrics.MENU_BODY_FONT,
            _inputService
        );
        seedInteractions.WithTextInputAudio(_soundEffectService);
        if (seedInteractions.Has(Widgets.Interactions.Input))
        {
            _createError = null;
        }

        IntRect randomRect = Widgets.Rect(
            x + INPUT_WIDTH + 8,
            seedY + 12,
            UiMetrics.SMALL_ICON_BUTTON_SIZE,
            UiMetrics.SMALL_ICON_BUTTON_SIZE
        );
        Widgets.IconButtonOptions randomOptions = new(SoundEffectService: _soundEffectService);
        if (ui.IconButton("Button_RandomSeed", randomRect, MenuGlyphs.DICE, randomOptions))
        {
            _createError = null;
            _seedTextBox.Text.Clear();
            _seedTextBox.Text.Append(Random.Shared.NextInt64());
        }
    }

    /// <summary>Draws the sector dossier panel.</summary>
    private void DrawDossier(UIBuilder<Material> ui, IntRect rect)
    {
        using (ui.MenuPanel(rect, _localization.GetRequired("ui.menu.sectorDossier")))
        {
            int padding = 20;
            string name = _saveNameTextBox.Text.ToString().Trim(_saveNameTrimChars);
            if (string.IsNullOrWhiteSpace(name))
            {
                name = _localization.GetRequired("ui.field.untitledSector");
            }

            ui.Label(
                name,
                Widgets.Rect(padding, 42, rect.Size.X - padding * 2, 26),
                UiMetrics.MENU_TITLE_FONT,
                UiTheme.Text,
                Anchors.Left | Anchors.Center
            );
            ui.Label(
                $"{_localization.GetRequired("ui.menu.mode")}: CREATIVE",
                Widgets.Rect(padding, 76, rect.Size.X - padding * 2, 18),
                UiMetrics.MENU_SMALL_FONT,
                UiTheme.TextMuted,
                Anchors.Left | Anchors.Center
            );
            ui.Label(
                $"{_localization.GetRequired("ui.menu.seed")}: {(_seedTextBox.Text.Length > 0 ? _seedTextBox.Text.ToString() : _localization.GetString("ui.field.random"))}",
                Widgets.Rect(padding, 96, rect.Size.X - padding * 2, 18),
                UiMetrics.MENU_SMALL_FONT,
                UiTheme.TextMuted,
                Anchors.Left | Anchors.Center
            );
            ui.Label(
                $"{_localization.GetRequired("ui.level.type")}: {_localization.GetRequired("ui.level.type.asteroids")}",
                Widgets.Rect(padding, 116, rect.Size.X - padding * 2, 18),
                UiMetrics.MENU_SMALL_FONT,
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

        const int titleY = 48;
        int feedWidth = Math.Min(480, layout.PageWidth - 80);
        int feedX = (layout.PageWidth - feedWidth) / 2;
        int feedY = titleY + 40;
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

            using (ui.MenuPanel(layout.Right, _localization.GetRequired("ui.menu.sectorInitialization")))
            {
                DrawCreateFeed(ui, feedX, titleY, feedY, feedWidth, feedBottom);
            }
        }
    }

    /// <summary>Draws the creation log, its cursor, and the closing divider.</summary>
    private void DrawCreateFeed(UIBuilder<Material> ui, int feedX, int titleY, int feedY, int feedWidth, int feedBottom)
    {
        ui.Label(
            _localization.GetRequired("ui.menu.creatingSector"),
            Widgets.Rect(feedX, titleY, feedWidth, 24),
            UiMetrics.MENU_BODY_FONT,
            UiTheme.Text,
            Anchors.Left | Anchors.Center
        );

        for (var i = 0; i < _createMessages.Count; i++)
        {
            DrawCreateMessage(ui, i, feedX, feedY, feedWidth);
        }

        DrawCreateCursor(ui, feedX, feedY);

        ui.Divider(
            Widgets.Rect(feedX, feedBottom, feedWidth, 1),
            UiTheme.Border
        );
    }

    /// <summary>Draws one fading line of the creation log.</summary>
    private void DrawCreateMessage(UIBuilder<Material> ui, int index, int feedX, int feedY, int feedWidth)
    {
        float fade = Math.Clamp(
            (float)((_createTime - _createMessageTimes[index]) / CREATE_MESSAGE_FADE_SECONDS),
            0f,
            1f
        );
        int lineY = feedY + index * CREATE_MESSAGE_ROW_HEIGHT;

        ui.Label(
            ">",
            Widgets.Rect(feedX, lineY, 12, 18),
            UiMetrics.MENU_SMALL_FONT,
            Fade(UiTheme.Accent, fade),
            Anchors.Left | Anchors.Center
        );
        ui.Label(
            _createMessages[index],
            Widgets.Rect(feedX + 14, lineY, feedWidth - 14, 18),
            UiMetrics.MENU_SMALL_FONT,
            Fade(UiTheme.TextMuted, fade),
            Anchors.Left | Anchors.Center
        );
    }

    /// <summary>Draws the log prompt and its blinking cursor below the last message.</summary>
    private void DrawCreateCursor(UIBuilder<Material> ui, int feedX, int feedY)
    {
        int cursorY = feedY + _createMessages.Count * CREATE_MESSAGE_ROW_HEIGHT;
        ui.Label(
            ">",
            Widgets.Rect(feedX, cursorY, 12, 18),
            UiMetrics.MENU_SMALL_FONT,
            UiTheme.Accent,
            Anchors.Left | Anchors.Center
        );

        if ((int)(_createTime * 2) % 2 != 0)
        {
            return;
        }

        using (ui.Element())
        {
            ui.Color = UiTheme.Accent;
            ui.Constraints = Widgets.RectConstraints(
                Widgets.Rect(feedX + 14, cursorY + 3, 8, 12)
            );
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
