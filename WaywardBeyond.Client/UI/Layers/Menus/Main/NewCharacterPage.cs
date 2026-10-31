using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Reef;
using Reef.UI;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Bodies;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Client.UI.Layers.Menus.Modal;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Characters;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

internal sealed class NewCharacterPage : IMenuPage<MenuPage>
{
    private const int DEFAULT_SPACER_POINTS = 18;
    private const int MIN_STAT = 1;
    private const int MAX_STAT = 8;

    private static readonly char[] _characterNameTrimChars = [' ', '\t', '.', '\n', '\r'];
    private static readonly string[] _statLabels =
    [
        "ui.label.character.strength",
        "ui.label.character.precision",
        "ui.label.character.awareness",
        "ui.label.character.charisma",
        "ui.label.character.education",
        "ui.label.character.resolve",
    ];
    private static readonly string[] _statDescriptions =
    [
        "ui.text.character.strength",
        "ui.text.character.precision",
        "ui.text.character.awareness",
        "ui.text.character.charisma",
        "ui.text.character.education",
        "ui.text.character.resolve",
    ];

    public MenuPage ID => MenuPage.NewCharacter;

    private readonly ILogger<NewCharacterPage> _logger;
    private readonly ICharacterStorage _characterStorage;
    private readonly CharacterSaveManager _characterSaveManager;
    private readonly SoundEffectService _soundEffectService;
    private readonly ILocalization _localization;
    private readonly IBodyDatabase _bodyDatabase;
    private readonly CharacterAssetService _characterAssetService;
    private readonly NameGenerator _nameGenerator;
    private readonly NotificationService _notificationService;
    private readonly ModalMenu _modalMenu;
    private readonly ConfirmModal _confirmModal;
    private readonly Randomizer _randomizer = new();
    private readonly NewCharacterView _view;

    private Menu<MenuPage>? _menu;
    private int _speciesIndex;
    private Gender _gender = Gender.Male;
    private Gender _rememberedGender = Gender.Male;
    private int _spacerPoints = DEFAULT_SPACER_POINTS;
    private int _focusedStat;
    private int _narrowTab;
    private int _strength = 1;
    private int _precision = 1;
    private int _awareness = 1;
    private int _charisma = 1;
    private int _education = 1;
    private int _resolve = 1;

    public NewCharacterPage(
        in ILogger<NewCharacterPage> logger,
        in ICharacterStorage characterStorage,
        in CharacterSaveManager characterSaveManager,
        in IInputService inputService,
        in SoundEffectService soundEffectService,
        in ILocalization localization,
        in IBodyDatabase bodyDatabase,
        in CharacterAssetService characterAssetService,
        in NameGenerator nameGenerator,
        in NotificationService notificationService,
        in ModalMenu modalMenu,
        in ConfirmModal confirmModal
    )
    {
        _logger = logger;
        _characterStorage = characterStorage;
        _characterSaveManager = characterSaveManager;
        _soundEffectService = soundEffectService;
        _localization = localization;
        _bodyDatabase = bodyDatabase;
        _characterAssetService = characterAssetService;
        _nameGenerator = nameGenerator;
        _notificationService = notificationService;
        _modalMenu = modalMenu;
        _confirmModal = confirmModal;

        _view = new NewCharacterView(localization)
        {
            Input = inputService,
            RandomNameRequested = RandomizeName,
            SpeciesStepRequested = StepSpecies,
            RandomSpeciesRequested = RandomizeSpecies,
            GenderStepRequested = StepGender,
            RandomGenderRequested = RandomizeGender,
            RandomPointsRequested = RandomizePoints,
            StatDecreaseRequested = DecreaseStat,
            StatIncreaseRequested = IncreaseStat,
            TabRequested = SelectTab,
            BackRequested = Back,
            CreateRequested = RequestCreate,
        };
    }

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        _menu = menu;
        EnsureValidSelection();
        _view.Render(ui, BuildState(ui));
        return Result.FromSuccess();
    }

    /// <summary>Collects the form values, selectors, stat rows, and validation shown this frame.</summary>
    private NewCharacterState BuildState(UIBuilder<Material> ui)
    {
        string name = CurrentName();
        bool nameMissing = string.IsNullOrWhiteSpace(name);
        bool nameTaken = !nameMissing && _characterStorage.GetAllCharacters().Any(character => character.Name == name);
        (string? status, bool danger, bool warning) = ResolveStatus(nameMissing, nameTaken);

        IReadOnlyList<string> speciesList = _bodyDatabase.Species;
        string species = CurrentSpecies();
        IReadOnlyList<Gender> genders = _bodyDatabase.GetGenders(species);

        return new NewCharacterState(
            _narrowTab,
            _spacerPoints,
            _characterAssetService.GetAppearanceMaterial(CurrentBodyId()),
            new SelectorState(
                L("ui.field.character.species"),
                species,
                speciesList.Count > 1,
                speciesList.Count > 1 && _speciesIndex > 0,
                speciesList.Count > 1 && _speciesIndex < speciesList.Count - 1
            ),
            new SelectorState(
                L("ui.field.character.gender"),
                genders.Count > 0 ? _view.GenderName(_gender) : string.Empty,
                genders.Count > 1,
                genders.Count > 1 && IndexOfGender(genders, _gender) > 0,
                genders.Count > 1 && IndexOfGender(genders, _gender) < genders.Count - 1
            ),
            BuildStatRows(),
            L("ui.label.info"),
            _view.InfoText(L(_statLabels[_focusedStat]), L(_statDescriptions[_focusedStat])),
            status,
            danger,
            warning,
            CanCreate: !nameMissing && !nameTaken
        );
    }

    /// <summary>Builds the six spacer stat rows.</summary>
    private List<CharacterStatRow> BuildStatRows()
    {
        int[] values = [_strength, _precision, _awareness, _charisma, _education, _resolve];
        var rows = new List<CharacterStatRow>(values.Length);
        for (var i = 0; i < values.Length; i++)
        {
            rows.Add(new CharacterStatRow(
                L(_statLabels[i]),
                values[i].ToString(),
                _focusedStat == i,
                values[i] > MIN_STAT,
                _spacerPoints > 0 && values[i] < MAX_STAT
            ));
        }

        return rows;
    }

    /// <summary>Gets the footer message and its tone for the current name and unspent points.</summary>
    private (string? Status, bool Danger, bool Warning) ResolveStatus(bool nameMissing, bool nameTaken)
    {
        if (nameMissing)
        {
            return (_localization.GetString("ui.message.name.required"), true, false);
        }

        if (nameTaken)
        {
            return (_localization.GetString("ui.message.name.taken"), true, false);
        }

        if (_spacerPoints == 0)
        {
            return (null, false, false);
        }

        return (
            string.Format(_localization.GetRequired("ui.message.character.unspentPoints"), _spacerPoints),
            false,
            true
        );
    }

    private string L(string key)
    {
        return _localization.GetRequired(key);
    }

    private void SelectTab(int tab)
    {
        _narrowTab = tab;
    }

    private void Back()
    {
        _soundEffectService.PlayInterfaceSound(WidgetInteraction.InterfaceSound.Click);
        _menu?.GoToPage(MenuPage.SelectCharacter);
    }

    private void StepSpecies(int delta)
    {
        IReadOnlyList<string> speciesList = _bodyDatabase.Species;
        if (speciesList.Count <= 1)
        {
            return;
        }

        _speciesIndex = Math.Clamp(_speciesIndex + delta, 0, speciesList.Count - 1);
        EnsureValidSelection();
        _soundEffectService.PlayInterfaceSound(
            delta > 0 ? WidgetInteraction.InterfaceSound.Increase : WidgetInteraction.InterfaceSound.Decrease
        );
    }

    private void StepGender(int delta)
    {
        IReadOnlyList<Gender> genders = _bodyDatabase.GetGenders(CurrentSpecies());
        if (genders.Count <= 1)
        {
            return;
        }

        int index = Math.Clamp(IndexOfGender(genders, _gender) + delta, 0, genders.Count - 1);
        SetGender(genders[index]);
        _soundEffectService.PlayInterfaceSound(
            delta > 0 ? WidgetInteraction.InterfaceSound.Increase : WidgetInteraction.InterfaceSound.Decrease
        );
    }

    private void DecreaseStat(int index)
    {
        _focusedStat = index;
        SetStat(index, ValueOf(index) - 1);
        _soundEffectService.PlayInterfaceSound(WidgetInteraction.InterfaceSound.Decrease);
    }

    private void IncreaseStat(int index)
    {
        _focusedStat = index;
        SetStat(index, ValueOf(index) + 1);
        _soundEffectService.PlayInterfaceSound(WidgetInteraction.InterfaceSound.Increase);
    }

    private int ValueOf(int index)
    {
        return index switch
        {
            0 => _strength,
            1 => _precision,
            2 => _awareness,
            3 => _charisma,
            4 => _education,
            _ => _resolve,
        };
    }

    /// <summary>Gets the trimmed character name the field currently holds.</summary>
    private string CurrentName()
    {
        return CharacterNames.Clamp(_view.NameState.Text.ToString().Trim(_characterNameTrimChars));
    }

    private void SetStat(int index, int value)
    {
        int oldValue = ValueOf(index);
        int clamped = Math.Clamp(value, MIN_STAT, MAX_STAT);
        int change = clamped - oldValue;
        if (change == 0)
        {
            return;
        }

        if (change > 0 && _spacerPoints - change < 0)
        {
            return;
        }

        switch (index)
        {
            case 0:
                _strength = clamped;
                break;
            case 1:
                _precision = clamped;
                break;
            case 2:
                _awareness = clamped;
                break;
            case 3:
                _charisma = clamped;
                break;
            case 4:
                _education = clamped;
                break;
            default:
                _resolve = clamped;
                break;
        }

        _spacerPoints -= change;
    }

    /// <summary>Creates the character, or asks for confirmation first while spacer points are unspent.</summary>
    private void RequestCreate()
    {
        if (_spacerPoints == 0)
        {
            TryCreate();
            return;
        }

        _modalMenu.GoToPage(_confirmModal.Create(
            TryCreate,
            _localization.GetRequired("ui.dialog.character.unspent"),
            string.Format(_localization.GetRequired("ui.dialog.character.unspent.body"), _spacerPoints),
            _localization.GetRequired("ui.button.character.create"),
            _localization.GetRequired("ui.button.cancel")
        ));
    }

    private void TryCreate()
    {
        string nameValue = CurrentName();
        if (string.IsNullOrWhiteSpace(nameValue))
        {
            _notificationService.Push(new Notification(_localization.GetRequired("ui.message.name.required")));
            return;
        }

        if (_characterStorage.GetAllCharacters().Any(character => character.Name == nameValue))
        {
            _notificationService.Push(new Notification(_localization.GetRequired("ui.message.name.taken")));
            return;
        }

        var character = new Character(
            WaywardBeyond.Version,
            Uuid.NewUuid().ToValue(),
            _Playtime: new Playtime(),
            nameValue,
            _strength,
            _precision,
            _awareness,
            _charisma,
            _education,
            _resolve,
            _Body: CurrentBodyId(),
            _ActiveInventorySlot: 0,
            _GameMode: GameMode.Creative,
            _Statistics: null,
            _Inventory: null
        );

        _soundEffectService.PlayInterfaceSound(WidgetInteraction.InterfaceSound.Click);
        Task.Run(() => SaveCharacter(character));
    }

    /// <summary>Saves the new character, then opens the character list.</summary>
    private void SaveCharacter(Character character)
    {
        Result saveResult = _characterStorage.SaveCharacter(character);
        if (saveResult)
        {
            _characterSaveManager.ActiveSave = character;
            _menu?.GoToPage(MenuPage.SelectCharacter);
            ResetState();
            return;
        }

        _notificationService.Push(new Notification(_localization.GetRequired("notification.character.createFailed")));
        _logger.LogError(
            saveResult.Exception,
            "Failed to save character \"{name}\" ({id}): {message}",
            character.Name,
            character.Uuid,
            saveResult.Message
        );
    }

    private void RandomizeName()
    {
        NameGenerator.Options options = new(
            TitleChance: 0.2f,
            FirstNameChance: 0.7f,
            LastNameChance: 1.0f,
            SubtitleChance: 0.1f,
            NicknameChance: 0.1f
        );

        string generatedName = _nameGenerator.Generate(key: CurrentBodyId(), options);
        _view.NameState.Text.Clear();
        _view.NameState.Text.Append(generatedName);
    }

    private void RandomizePoints()
    {
        if (_spacerPoints == 0)
        {
            ResetAttributes();
        }

        while (_spacerPoints > 0)
        {
            int[] candidates = Enumerable.Range(0, 6)
                .Where(index => ValueOf(index) < MAX_STAT)
                .ToArray();

            if (candidates.Length == 0)
            {
                break;
            }

            int attributeIndex = candidates[_randomizer.NextInt(0, candidates.Length)];
            SetStat(attributeIndex, ValueOf(attributeIndex) + 1);
        }
    }

    private void ResetState()
    {
        _speciesIndex = 0;
        _gender = Gender.Male;
        _rememberedGender = Gender.Male;
        EnsureValidSelection();
        _view.NameState.Text.Clear();
        ResetAttributes();
    }

    private void ResetAttributes()
    {
        _spacerPoints = DEFAULT_SPACER_POINTS;
        _strength = 1;
        _precision = 1;
        _awareness = 1;
        _charisma = 1;
        _education = 1;
        _resolve = 1;
        _focusedStat = 0;
    }

    /// <summary>Gets the body ID for the current species and gender.</summary>
    private string CurrentBodyId()
    {
        return _characterAssetService.GetBodyId(CurrentSpecies(), _gender);
    }

    /// <summary>Clamps the species index and keeps the gender valid for the current species.</summary>
    private void EnsureValidSelection()
    {
        IReadOnlyList<string> speciesList = _bodyDatabase.Species;
        if (speciesList.Count == 0)
        {
            _speciesIndex = 0;
            return;
        }

        _speciesIndex = Math.Clamp(_speciesIndex, 0, speciesList.Count - 1);
        IReadOnlyList<Gender> genders = _bodyDatabase.GetGenders(speciesList[_speciesIndex]);
        if (genders.Contains(_gender))
        {
            return;
        }

        if (genders.Contains(_rememberedGender))
        {
            _gender = _rememberedGender;
        }
        else
        {
            _gender = genders.Count > 0 ? genders[0] : Gender.Neutral;
        }
    }

    /// <summary>Selects a gender and remembers it while it is not neutral.</summary>
    private void SetGender(Gender gender)
    {
        _gender = gender;
        if (gender != Gender.Neutral)
        {
            _rememberedGender = gender;
        }
    }

    /// <summary>Picks a different species at random, then keeps the gender valid.</summary>
    private void RandomizeSpecies()
    {
        IReadOnlyList<string> speciesList = _bodyDatabase.Species;
        if (speciesList.Count <= 1)
        {
            return;
        }

        int nextIndex;
        do
        {
            nextIndex = _randomizer.NextInt(0, speciesList.Count);
        } while (nextIndex == _speciesIndex);

        _speciesIndex = nextIndex;
        EnsureValidSelection();
        _soundEffectService.PlayInterfaceSound(WidgetInteraction.InterfaceSound.Toggle);
    }

    /// <summary>Picks a different gender for the current species at random.</summary>
    private void RandomizeGender()
    {
        IReadOnlyList<Gender> genders = _bodyDatabase.GetGenders(CurrentSpecies());
        if (genders.Count <= 1)
        {
            return;
        }

        int currentIndex = IndexOfGender(genders, _gender);
        int nextIndex;
        do
        {
            nextIndex = _randomizer.NextInt(0, genders.Count);
        } while (nextIndex == currentIndex);

        SetGender(genders[nextIndex]);
        _soundEffectService.PlayInterfaceSound(WidgetInteraction.InterfaceSound.Toggle);
    }

    /// <summary>Gets the current species ID, or an empty string when no body is loaded.</summary>
    private string CurrentSpecies()
    {
        IReadOnlyList<string> speciesList = _bodyDatabase.Species;
        return speciesList.Count > _speciesIndex ? speciesList[_speciesIndex] : string.Empty;
    }

    /// <summary>Gets the position of a gender in a list, or 0 when it is absent.</summary>
    private static int IndexOfGender(IReadOnlyList<Gender> genders, Gender gender)
    {
        for (var i = 0; i < genders.Count; i++)
        {
            if (genders[i] == gender)
            {
                return i;
            }
        }

        return 0;
    }
}
