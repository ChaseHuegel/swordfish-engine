using System;
using System.Numerics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.UI.Components;
using WaywardBeyond.Client.UI.Theme;
using WaywardBeyond.Data;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

/// <summary>Forwards galaxy form interactions and formats galaxy values.</summary>
internal partial class NewSaveView : UiViewBase
{
    private static readonly char[] TrimChars = [' ', '\t', '.', '\n', '\r'];

    private readonly ILocalization _localization;
    private readonly string[] _modeOptions;

    /// <summary>Initializes the galaxy form view.</summary>
    public NewSaveView(ILocalization localization, ScrollAreaState? formScroll = null) : base(localization)
    {
        _localization = localization;
        FormScroll = formScroll ?? new ScrollAreaState();
        _modeOptions =
        [
            _localization.GetModeName(GameMode.Creative),
            _localization.GetModeName(GameMode.Adventure),
        ];
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

    /// <summary>Gets the form pane scroll state.</summary>
    public ScrollAreaState FormScroll { get; }

    /// <summary>Gets the localized labels shown in the game mode selector.</summary>
    public string[] ModeOptions => _modeOptions;

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

    /// <summary>Raised when a default game mode option is selected.</summary>
    public Action<GameMode>? GameModeSelected { get; set; }

    /// <summary>Raised when a narrow layout tab is clicked.</summary>
    public Action<int>? TabRequested { get; set; }

    /// <summary>Raised when cancel is clicked.</summary>
    public Action? CancelRequested { get; set; }

    /// <summary>Raised when create is clicked.</summary>
    public Action? CreateRequested { get; set; }

    partial void OnRandomName() => RandomNameRequested?.Invoke();

    partial void OnRandomSeed() => RandomSeedRequested?.Invoke();

    /// <summary>Forwards a game mode choice selection.</summary>
    public void OnGameModeSelected(string id, int index)
    {
        GameModeSelected?.Invoke(index == 1 ? GameMode.Adventure : GameMode.Creative);
    }

    partial void OnSelectTab(int tab) => TabRequested?.Invoke(tab);

    partial void OnCancel() => CancelRequested?.Invoke();

    partial void OnCreate() => CreateRequested?.Invoke();

    /// <summary>Gets the selector index for a game mode.</summary>
    public static int ModeIndex(GameMode mode)
    {
        return mode == GameMode.Adventure ? 1 : 0;
    }

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
    public string ModeLine(GameMode mode)
    {
        return Labeled("ui.label.level.mode", _localization.GetModeName(mode));
    }

    /// <summary>Gets the localized summary of the selected game mode.</summary>
    public string ModeDescription(GameMode mode)
    {
        return mode == GameMode.Adventure
            ? L("ui.text.level.mode.adventure")
            : L("ui.text.level.mode.creative");
    }

    /// <summary>Formats a labeled detail line.</summary>
    public string Labeled(string labelKey, string value)
    {
        return L(labelKey) + ": " + value;
    }

    private const double LINE_FADE_SECONDS = 0.3d;

    /// <summary>Gets the accent color of a creating log line, faded by its age.</summary>
    public static Vector4 FadeAccent(double age)
    {
        return DynamicColors.Fade(WbPalette.Accent, FadeAmount(age));
    }

    /// <summary>Gets the muted color of a creating log line, faded by its age.</summary>
    public static Vector4 FadeMuted(double age)
    {
        return DynamicColors.Fade(WbPalette.TextMuted, FadeAmount(age));
    }

    /// <summary>Gets whether the creation caret is in its visible blink.</summary>
    public static bool CursorOn(double elapsed)
    {
        return (int)(elapsed * 2d) % 2 == 0;
    }

    private static float FadeAmount(double age)
    {
        return (float)Math.Clamp(age / LINE_FADE_SECONDS, 0d, 1d);
    }
}
