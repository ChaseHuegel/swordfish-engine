using System;
using System.Numerics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

/// <summary>Forwards galaxy form interactions and formats galaxy values.</summary>
internal partial class NewSaveView : UiViewBase
{
    private static readonly char[] TrimChars = [' ', '\t', '.', '\n', '\r'];

    private readonly ILocalization _localization;

    /// <summary>Initializes the galaxy form view.</summary>
    public NewSaveView(ILocalization localization) : base(localization)
    {
        _localization = localization;
        NameState = new TextBoxState(
            initialValue: string.Empty,
            options: new TextBoxState.Options(
                Placeholder: L("ui.field.level.name.placeholder"),
                MaxCharacters: 20,
                DisallowedCharacters: ['\0', '\\', '/', ':', '*', '?', '"', '<', '>', '|']
            )
        );
        SeedState = new TextBoxState(
            initialValue: string.Empty,
            options: new TextBoxState.Options(
                Placeholder: L("ui.field.level.seed.placeholder"),
                MaxCharacters: 40
            )
        );
    }

    /// <summary>The editable galaxy name state.</summary>
    public TextBoxState NameState;

    /// <summary>The editable seed state.</summary>
    public TextBoxState SeedState;

    /// <summary>Gets or sets the input service the editable fields read.</summary>
    public IInputService Input { get; set; } = null!;

    /// <summary>Raised when the name randomizer is clicked.</summary>
    public Action? RandomNameRequested { get; set; }

    /// <summary>Raised when the seed randomizer is clicked.</summary>
    public Action? RandomSeedRequested { get; set; }

    /// <summary>Raised when a narrow layout tab is clicked.</summary>
    public Action<int>? TabRequested { get; set; }

    /// <summary>Raised when cancel is clicked.</summary>
    public Action? CancelRequested { get; set; }

    /// <summary>Raised when create is clicked.</summary>
    public Action? CreateRequested { get; set; }

    partial void OnRandomName() => RandomNameRequested?.Invoke();

    partial void OnRandomSeed() => RandomSeedRequested?.Invoke();

    partial void OnSelectTab(int tab) => TabRequested?.Invoke(tab);

    partial void OnCancel() => CancelRequested?.Invoke();

    partial void OnCreate() => CreateRequested?.Invoke();

    /// <summary>Gets the trimmed galaxy name, or the untitled label.</summary>
    public string DisplayName(string name)
    {
        string trimmed = name.Trim(TrimChars);
        return string.IsNullOrWhiteSpace(trimmed) ? L("ui.label.level.untitled") : trimmed;
    }

    /// <summary>Gets the seed shown in the detail panel.</summary>
    public string DisplaySeed(string seed)
    {
        return seed.Length > 0 ? seed : L("ui.label.level.seed.random");
    }

    /// <summary>Gets the created galaxy mode line.</summary>
    public string ModeLine()
    {
        return L("ui.label.level.mode") + ": " + "Creative";
    }

    /// <summary>Formats a labeled detail line.</summary>
    public string Labeled(string labelKey, string value)
    {
        return L(labelKey) + ": " + value;
    }

    /// <summary>Gets the accent color of a creating log line, faded by its age.</summary>
    public static Vector4 FadeAccent(float fade)
    {
        return DynamicColors.Fade(DynamicColors.Accent, Math.Clamp(fade, 0f, 1f));
    }

    /// <summary>Gets the muted color of a creating log line, faded by its age.</summary>
    public static Vector4 FadeMuted(float fade)
    {
        return DynamicColors.Fade(DynamicColors.TextMuted, Math.Clamp(fade, 0f, 1f));
    }
}
