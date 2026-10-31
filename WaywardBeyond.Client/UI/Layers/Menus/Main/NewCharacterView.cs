using System;
using Reef.UI;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using WaywardBeyond.Bodies;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Data.Characters;

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

    /// <summary>Steps a selector by one option.</summary>
    public void OnStep(string id, int delta)
    {
        if (string.Equals(id, "species", StringComparison.Ordinal))
        {
            SpeciesStepRequested?.Invoke(delta);
            return;
        }

        GenderStepRequested?.Invoke(delta);
    }

    partial void OnRandomGender() => RandomGenderRequested?.Invoke();

    partial void OnRandomPoints() => RandomPointsRequested?.Invoke();

    partial void OnStatMinus(int index) => StatDecreaseRequested?.Invoke(index);

    partial void OnStatPlus(int index) => StatIncreaseRequested?.Invoke(index);

    partial void OnSelectTab(int tab) => TabRequested?.Invoke(tab);

    partial void OnBack() => BackRequested?.Invoke();

    partial void OnCreate() => CreateRequested?.Invoke();

    /// <summary>Emphasizes the first letter of a stat name, in orange while focused.</summary>
    public static string StatText(string label, bool focused)
    {
        if (label.Length == 0)
        {
            return label;
        }

        return $"#{(focused ? "orange" : "cyan")}{label[0]}#R{label[1..]}";
    }

    /// <summary>Formats the info box body with its emphasized stat name.</summary>
    public string InfoText(string name, string description)
    {
        return $"#F2994AFF {name}: #R {description}";
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

    /// <summary>Gets a localized label, or the key when the string is missing.</summary>
    public string Label(string key)
    {
        return _localization.GetRequired(key);
    }
}
