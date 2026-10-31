using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Localization;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

internal sealed class NewSavePage : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.NewSave;

    private const int MAX_CREATE_MESSAGES = 5;
    private const float CREATE_MESSAGE_INTERVAL_SECONDS = 2f;
    private const float CREATE_MESSAGE_FADE_SECONDS = 0.3f;
    private const string CREATE_MESSAGE_TAG = "game_create";
    private static readonly char[] _saveNameTrimChars = [' ', '\t', '.', '\n', '\r'];

    private readonly GameSaveService _gameSaveService;
    private readonly GameSaveManager _gameSaveManager;
    private readonly SoundEffectService _soundEffectService;
    private readonly ILocalization _localization;
    private readonly NameGenerator _nameGenerator;
    private readonly IAssetDatabase<LocalizedTags> _localizedTagDatabase;
    private readonly SeedPhraseService _seedPhraseService;
    private readonly Randomizer _randomizer = new();
    private readonly List<string> _createMessages = [];
    private readonly List<double> _createMessageTimes = [];
    private readonly NewSaveView _view;

    private Menu<MenuPage>? _menu;
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
        in IAssetDatabase<LocalizedTags> localizedTagDatabase,
        in SeedPhraseService seedPhraseService
    )
    {
        _gameSaveService = gameSaveService;
        _gameSaveManager = gameSaveManager;
        _soundEffectService = soundEffectService;
        _localization = localization;
        _nameGenerator = nameGenerator;
        _localizedTagDatabase = localizedTagDatabase;
        _seedPhraseService = seedPhraseService;

        _view = new NewSaveView(localization)
        {
            Input = inputService,
            RandomNameRequested = OnRandomName,
            RandomSeedRequested = OnRandomSeed,
            TabRequested = OnSelectTab,
            CancelRequested = OnCancel,
            CreateRequested = TryCreate,
        };
    }

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        if (!_gameSaveService.CanCreateSave)
        {
            menu.GoToPage(MenuPage.Home);
            return Result.FromSuccess();
        }

        _menu = menu;

        Task<GameSave?>? createTask = _createTask;
        if (createTask != null)
        {
            return RenderCreateTask(ui, menu, createTask, delta);
        }

        _view.Render(ui, BuildState());
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
            _view.Render(ui, BuildCreateState(delta));
            return Result.FromSuccess();
        }

        if (createTask.IsFaulted)
        {
            _createTask = null;
            _createError = _localization.GetRequired("ui.message.level.createFailed");
            return Result.FromSuccess();
        }

        GameSave? created = createTask.Result;
        _createTask = null;
        if (created == null)
        {
            _createError = _localization.GetRequired("ui.message.level.createFailed");
            return Result.FromSuccess();
        }

        _gameSaveManager.ActiveSave = created;
        _view.NameState.Text.Clear();
        _view.SeedState.Text.Clear();
        menu.GoToPage(MenuPage.SelectCharacter);
        return Result.FromSuccess();
    }

    /// <summary>Collects the form values, validation, and detail lines shown this frame.</summary>
    private NewSaveState BuildState()
    {
        string currentName = CurrentName();
        bool nameMissing = string.IsNullOrWhiteSpace(currentName);
        bool duplicateName = !nameMissing && _gameSaveService.GetSaves().Any(save => save.Name == currentName);
        bool seedMissing = string.IsNullOrWhiteSpace(_view.SeedState.Text.ToString());
        (string? message, bool danger, bool warning) = ResolveStatus(nameMissing, duplicateName, seedMissing);

        string seed = _view.SeedState.Text.ToString();
        return new NewSaveState(
            Creating: false,
            _view.DisplayName(currentName),
            _view.ModeLine(),
            _view.Labeled("ui.label.level.seed", _view.DisplaySeed(seed)),
            _view.Labeled("ui.label.level.type", L("ui.label.level.type.asteroids")),
            CanCreate: !nameMissing && !duplicateName,
            message,
            danger,
            warning,
            _narrowTab,
            [],
            CursorOn: false
        );
    }

    /// <summary>Collects the creation log shown while a save is being created.</summary>
    private NewSaveState BuildCreateState(double delta)
    {
        _createTime += delta;
        if (_createTime >= _nextCreateMessageTime)
        {
            AddCreateMessage();
        }

        var lines = new List<CreateLineState>(_createMessages.Count);
        for (var i = 0; i < _createMessages.Count; i++)
        {
            float fade = (float)Math.Clamp(
                (_createTime - _createMessageTimes[i]) / CREATE_MESSAGE_FADE_SECONDS,
                0d,
                1d
            );
            lines.Add(new CreateLineState(_createMessages[i], fade));
        }

        return new NewSaveState(
            Creating: true,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            CanCreate: false,
            Status: null,
            StatusDanger: false,
            StatusWarning: false,
            Tab: 0,
            lines,
            CursorOn: (int)(_createTime * 2) % 2 == 0
        );
    }

    private string L(string key)
    {
        return _localization.GetRequired(key);
    }

    private string CurrentName()
    {
        return _view.NameState.Text.ToString().Trim(_saveNameTrimChars);
    }

    /// <summary>Gets the footer message and its tone for the current name and seed.</summary>
    private (string? Message, bool Danger, bool Warning) ResolveStatus(
        bool nameMissing,
        bool duplicateName,
        bool seedMissing
    )
    {
        if (_createError != null)
        {
            return (_createError, true, false);
        }

        if (nameMissing)
        {
            return (_localization.GetString("ui.message.name.required"), true, false);
        }

        if (duplicateName)
        {
            return (_localization.GetString("ui.message.name.taken"), true, false);
        }

        if (!seedMissing)
        {
            return (null, false, false);
        }

        return (_localization.GetString("ui.message.level.randomSeed"), false, true);
    }

    private void OnRandomName()
    {
        _createError = null;

        NameGenerator.Options options = new(
            TitleChance: 0.7f,
            FirstNameChance: 0.7f,
            LastNameChance: 1.0f,
            SubtitleChance: 0.9f,
            NicknameChance: 0.7f
        );

        string generatedName = _nameGenerator.Generate(key: "save", options);
        var attempts = 0;
        while (attempts < 10
               && (generatedName.Length > _view.NameState.Settings.MaxCharacters
                   || generatedName == _view.NameState.Text.ToString()))
        {
            generatedName = _nameGenerator.Generate(key: "save", options);
            attempts++;
        }

        _view.NameState.Text.Clear();
        _view.NameState.Text.Append(generatedName);
    }

    private void OnRandomSeed()
    {
        _createError = null;
        _view.SeedState.Text.Clear();
        _view.SeedState.Text.Append(_seedPhraseService.SeedToPhrase(Random.Shared.NextInt64()));
    }

    private void OnSelectTab(int tab)
    {
        _narrowTab = tab;
    }

    private void OnCancel()
    {
        _soundEffectService.PlayInterfaceSound(WidgetInteraction.InterfaceSound.Click);
        _menu?.GoToPage(MenuPage.Home);
    }

    private void TryCreate()
    {
        string saveName = CurrentName();
        if (string.IsNullOrWhiteSpace(saveName) || _gameSaveService.GetSaves().Any(save => save.Name == saveName))
        {
            _createError = string.IsNullOrWhiteSpace(saveName)
                ? _localization.GetRequired("ui.message.name.required")
                : _localization.GetRequired("ui.message.name.taken");
            return;
        }

        string seedValue = _view.SeedState.Text.ToString();
        string seed = string.IsNullOrWhiteSpace(seedValue)
            ? _seedPhraseService.SeedToPhrase(Random.Shared.NextInt64())
            : seedValue;
        _createError = null;
        _createTime = 0;
        _nextCreateMessageTime = 0;
        _lastCreateMessage = string.Empty;
        _createMessages.Clear();
        _createMessageTimes.Clear();
        _soundEffectService.PlayInterfaceSound(WidgetInteraction.InterfaceSound.Click);
        _createTask = Task.Run(() => _gameSaveService.CreateSaveAsync(new GameOptions(saveName, seed)));
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
}
