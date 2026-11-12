using System;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using WaywardBeyond.Bodies;
using WaywardBeyond.Client.Extensions;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

/// <summary>Forwards colonist form interactions and formats colonist values.</summary>
internal partial class NewCharacterView : UiViewBase
{
    private readonly ILocalization _localization;

    /// <summary>Initializes the colonist form view.</summary>
    public NewCharacterView(ILocalization localization) : base(localization)
    {
        _localization = localization;
        NameState = new TextBoxState(
            initialValue: string.Empty,
            options: new TextBoxState.Options(
                Placeholder: L("ui.field.character.name.placeholder"),
                MaxCharacters: 20,
                DisallowedCharacters: ['\0', '\\', '/', ':', '*', '?', '<', '>', '|']
            )
        );
    }

    /// <summary>The editable colonist name state.</summary>
    public TextBoxState NameState;

    /// <summary>Gets or sets the input service the editable field reads.</summary>
    public IInputService Input { get; set; } = null!;

    /// <summary>Raised when the name randomizer is clicked.</summary>
    public Action? RandomNameRequested { get; set; }

    /// <summary>Raised when the species stepper moves.</summary>
    public Action<int>? SpeciesStepRequested { get; set; }

    /// <summary>Raised when the species randomizer is clicked.</summary>
    public Action? RandomSpeciesRequested { get; set; }

    /// <summary>Raised when the gender stepper moves.</summary>
    public Action<int>? GenderStepRequested { get; set; }

    /// <summary>Raised when the gender randomizer is clicked.</summary>
    public Action? RandomGenderRequested { get; set; }

    /// <summary>Raised when the spacer point randomizer is clicked.</summary>
    public Action? RandomPointsRequested { get; set; }

    /// <summary>Raised when a stat row steps down.</summary>
    public Action<int>? StatDecreaseRequested { get; set; }

    /// <summary>Raised when a stat row steps up.</summary>
    public Action<int>? StatIncreaseRequested { get; set; }

    /// <summary>Raised when a narrow layout tab is clicked.</summary>
    public Action<int>? TabRequested { get; set; }

    /// <summary>Raised when back is clicked.</summary>
    public Action? BackRequested { get; set; }

    /// <summary>Raised when create is clicked.</summary>
    public Action? CreateRequested { get; set; }

    partial void OnRandomName() => RandomNameRequested?.Invoke();

    partial void OnRandomSpecies() => RandomSpeciesRequested?.Invoke();

    /// <summary>Steps the species selector.</summary>
    public void OnSpeciesStep(string id, int delta) => SpeciesStepRequested?.Invoke(delta);

    /// <summary>Steps the gender selector.</summary>
    public void OnGenderStep(string id, int delta) => GenderStepRequested?.Invoke(delta);

    partial void OnRandomGender() => RandomGenderRequested?.Invoke();

    partial void OnRandomPoints() => RandomPointsRequested?.Invoke();

    partial void OnStatMinus(int index) => StatDecreaseRequested?.Invoke(index);

    partial void OnStatPlus(int index) => StatIncreaseRequested?.Invoke(index);

    partial void OnSelectTab(int tab) => TabRequested?.Invoke(tab);

    partial void OnBack() => BackRequested?.Invoke();

    partial void OnCreate() => CreateRequested?.Invoke();

    /// <summary>Emphasizes the first letter of a stat name, in orange while focused.</summary>
    public string StatText(string labelKey, bool focused)
    {
        string label = L(labelKey);
        if (label.Length == 0)
        {
            return label;
        }

        return $"#{(focused ? "orange" : "cyan")}{label[0]}#R{label[1..]}";
    }

    /// <summary>Formats the info box body with its emphasized stat name.</summary>
    public string InfoText(string nameKey, string descriptionKey)
    {
        return $"#F2994AFF {L(nameKey)}: #R {L(descriptionKey)}";
    }

    /// <summary>Formats the footer status line, or returns an empty string when there is none.</summary>
    public string StatusText(string key, string value)
    {
        if (key.Length == 0)
        {
            return string.Empty;
        }

        string format = L(key);
        return value.Length > 0 ? string.Format(format, value) : format;
    }

    /// <summary>Gets the localized gender name.</summary>
    public string GenderName(Gender gender)
    {
        return gender switch
        {
            Gender.Male => L("body.gender.male"),
            Gender.Female => L("body.gender.female"),
            _ => L("body.gender.neutral"),
        };
    }

    /// <summary>Gets the localized species name.</summary>
    public string SpeciesName(string species)
    {
        return _localization.GetSpeciesName(species);
    }
}
