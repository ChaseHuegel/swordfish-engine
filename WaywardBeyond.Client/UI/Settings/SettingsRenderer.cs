using System;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>Draws the settings page and applies widget interactions.</summary>
internal sealed class SettingsRenderer(
    ILocalization localization,
    SoundEffectService soundEffectService,
    IInputService inputService
)
{
    private readonly ILocalization _localization = localization;
    private readonly SoundEffectService _soundEffectService = soundEffectService;
    private readonly IInputService _inputService = inputService;

    private UIBuilder<Material>? _ui;
    private SettingsLayout _layout;
    private double _lastSliderSoundTime;

    private UIBuilder<Material> Ui => _ui ?? throw new InvalidOperationException("The layout must be set before drawing.");

    /// <summary>Sets the builder and layout for the current frame.</summary>
    public void SetLayout(UIBuilder<Material> ui, in SettingsLayout layout)
    {
        _ui = ui;
        _layout = layout;
    }

    /// <summary>Draws the category tray and returns true when the selected category changes.</summary>
    public bool DrawTray(SettingsCategory[] categories, ref int categoryIndex)
    {
        IntRect tray = _layout.Tray;
        int padding = SettingsMetrics.TRAY_PADDING;
        int width = tray.Size.X - padding * 2;

        Ui.Label(
            L("ui.settings.categories"),
            Widgets.Rect(padding, 10, width, 20),
            UiMetrics.SMALL_FONT,
            UiTheme.TextMuted,
            Anchors.Center
        );
        Ui.Divider(Widgets.Rect(padding, SettingsMetrics.TRAY_HEADER_HEIGHT - 1, width, 1), UiTheme.BorderInner);

        int y = SettingsMetrics.TRAY_HEADER_HEIGHT + 8;
        var changed = false;

        for (var i = 0; i < categories.Length; i++)
        {
            SettingsCategory category = categories[i];
            IntRect itemRect = Widgets.Rect(padding, y, width, SettingsMetrics.TRAY_ITEM_HEIGHT);
            bool selected = i == categoryIndex;
            string itemId = $"Settings.Category.{category.Id}";
            string label = L(category.TitleKey);

            using (Ui.Element(itemId))
            {
                bool clicked = Ui.Clicked();
                bool hovering = Ui.Hovering();
                DrawCategoryItem(itemRect, label, selected, hovering);

                if (clicked && !selected)
                {
                    categoryIndex = i;
                    changed = true;
                    Play(Widgets.InterfaceSound.Toggle);
                }
            }

            y += SettingsMetrics.TRAY_ITEM_HEIGHT + SettingsMetrics.TRAY_ITEM_GAP;
        }

        return changed;
    }

    /// <summary>Draws the horizontal category strip and returns true when the selected category changes.</summary>
    public bool DrawTabs(SettingsCategory[] categories, ref int categoryIndex)
    {
        IntRect tabs = _layout.Tabs;
        const int gap = 8;
        int tabWidth = (tabs.Size.X - gap * (categories.Length - 1)) / Math.Max(1, categories.Length);

        if (tabWidth <= 0)
        {
            return false;
        }

        var changed = false;

        using (Ui.Element())
        {
            Ui.LayoutDirection = LayoutDirection.None;
            Ui.Color = UiTheme.PanelBackground;
            Ui.Border = new BorderStyle
            {
                Thickness = 1,
                Color = UiTheme.Border,
            };
            Ui.Constraints = Widgets.RectConstraints(tabs);
            Ui.ClipConstraints = Widgets.RectConstraints(tabs);

            for (var i = 0; i < categories.Length; i++)
            {
                SettingsCategory category = categories[i];
                IntRect tabRect = Widgets.Rect(i * (tabWidth + gap), 0, tabWidth, tabs.Size.Y);
                bool selected = i == categoryIndex;
                string itemId = $"Settings.Tab.{category.Id}";

                using (Ui.Element(itemId))
                {
                    bool clicked = Ui.Clicked();
                    bool hovering = Ui.Hovering();
                    DrawCategoryItem(tabRect, L(category.TitleKey), selected, hovering, UiMetrics.SMALL_FONT);

                    if (clicked && !selected)
                    {
                        categoryIndex = i;
                        changed = true;
                        Play(Widgets.InterfaceSound.Toggle);
                    }
                }
            }
        }

        return changed;
    }

    /// <summary>Draws the page footer and reports the clicked action.</summary>
    public void DrawFooter(bool dirty, string? status, out bool cancelClicked, out bool applyClicked)
    {
        IntRect footer = _layout.Footer;
        IntRect cancelRect = Widgets.Rect(
            footer.Left,
            footer.Top + 4,
            SettingsMetrics.CANCEL_BUTTON_WIDTH,
            SettingsMetrics.FOOTER_BUTTON_HEIGHT
        );
        IntRect applyRect = Widgets.Rect(
            footer.Right - SettingsMetrics.APPLY_BUTTON_WIDTH,
            footer.Top + 4,
            SettingsMetrics.APPLY_BUTTON_WIDTH,
            SettingsMetrics.FOOTER_BUTTON_HEIGHT
        );

        cancelClicked = Ui.ActionButton(
            "Settings.Cancel",
            cancelRect,
            dirty ? L("ui.settings.cancel") : L("ui.button.return"),
            ButtonVariant.Secondary
        );
        applyClicked = Ui.ActionButton(
            "Settings.Apply",
            applyRect,
            L("ui.settings.apply"),
            ButtonVariant.Primary,
            dirty
        );

        if (dirty)
        {
            Ui.Label(
                L("ui.settings.unsaved"),
                Widgets.Rect(applyRect.Left - 110, footer.Top + 4, 100, SettingsMetrics.FOOTER_BUTTON_HEIGHT),
                UiMetrics.SMALL_FONT,
                UiTheme.Primary,
                Anchors.Right | Anchors.Center
            );
        }

        if (status != null && !dirty)
        {
            Ui.Label(
                status,
                Widgets.Rect(
                    footer.Left + SettingsMetrics.CANCEL_BUTTON_WIDTH + 20,
                    footer.Top + 4,
                    footer.Size.X - SettingsMetrics.CANCEL_BUTTON_WIDTH - SettingsMetrics.APPLY_BUTTON_WIDTH - 60,
                    SettingsMetrics.FOOTER_BUTTON_HEIGHT
                ),
                UiMetrics.SMALL_FONT,
                UiTheme.Success,
                Anchors.Center
            );
        }
    }

    /// <summary>Draws the active category pane and updates the scroll offset.</summary>
    public void DrawPane(SettingsCategory category, ref int scrollY)
    {
        IntRect pane = _layout.Pane;
        int padding = 24;
        int contentWidth = pane.Size.X - padding * 2;

        Ui.Label(
            L(category.TitleKey),
            Widgets.Rect(padding, 14, contentWidth, 28),
            UiMetrics.PANE_TITLE_FONT,
            UiTheme.Text,
            Anchors.Left | Anchors.Center
        );
        Ui.Divider(Widgets.Rect(padding, 62, contentWidth, 1), UiTheme.BorderInner);

        Ui.ScrollArea(
            LocalRect(_layout.List),
            category.ContentHeight,
            ref scrollY,
            _inputService,
            LocalRect(_layout.Scrollbar),
            _ =>
            {
                var rowY = 0;
                for (var i = 0; i < category.Rows.Length; i++)
                {
                    SettingsRow row = category.Rows[i];
                    row.Render(this, rowY);
                    rowY += row.Height;
                }
            }
        );
    }

    /// <summary>Draws a section header row.</summary>
    public void DrawSection(string labelKey, int y)
    {
        int width = _layout.List.Size.X;
        Ui.Label(
            L(labelKey),
            Widgets.Rect(0, y + 8, width, 20),
            UiMetrics.SECTION_FONT,
            UiTheme.TextMuted,
            Anchors.Left | Anchors.Center
        );
        Ui.Divider(Widgets.Rect(0, y + UiMetrics.SECTION_HEIGHT - 1, width, 1), UiTheme.BorderInner);
    }

    /// <summary>Draws an on/off row.</summary>
    public void DrawToggle(ToggleEntry entry, int y)
    {
        DrawEntryLabel(entry, y);
        IntRect control = ControlRect(y);

        ControlResult<bool> result = Ui.Toggle(
            entry.Id,
            control,
            entry.Value,
            L("ui.settings.on"),
            L("ui.settings.off"),
            entry.CanEdit
        );

        if (result.Changed)
        {
            entry.SetValue(result.Value);
            Play(Widgets.InterfaceSound.Toggle);
        }
    }

    /// <summary>Draws a radio-like choice row.</summary>
    public void DrawChoice<T>(ChoiceEntry<T> entry, int y)
    {
        DrawEntryLabel(entry, y);
        IntRect control = ControlRect(y);

        ControlResult<T> result = Ui.SegmentedControl(
            entry.Id,
            control,
            entry.Options,
            entry.Value,
            entry.GetOptionLabel,
            entry.CanEdit
        );

        if (result.Changed)
        {
            entry.SetValue(result.Value);
            Play(Widgets.InterfaceSound.Toggle);
        }
    }

    /// <summary>Draws a discrete stepper row.</summary>
    public void DrawStepper<T>(StepperEntry<T> entry, int y)
    {
        DrawEntryLabel(entry, y);
        IntRect control = ControlRect(y);
        int index = entry.IndexOf(entry.Value);

        ControlResult<int> result = Ui.Stepper(
            entry.Id,
            control,
            entry.Options,
            index,
            entry.Format,
            entry.CanEdit
        );

        if (!result.Changed || result.Value < 0 || result.Value >= entry.Options.Count)
        {
            return;
        }

        entry.SetValue(entry.Options[result.Value]);
        if (result.Value > index)
        {
            Play(Widgets.InterfaceSound.Increase);
        }
        else
        {
            Play(Widgets.InterfaceSound.Decrease);
        }
    }

    /// <summary>Draws a slider row.</summary>
    public void DrawSlider(SliderEntry entry, int y)
    {
        DrawEntryLabel(entry, y);
        IntRect control = ControlRect(y);

        ControlResult<float> result = Ui.Slider(
            entry.Id,
            control,
            entry.Value,
            entry.Min,
            entry.Max,
            entry.Step,
            entry.Format,
            entry.CanEdit
        );

        if (result.Changed)
        {
            entry.SetValue(result.Value);
            PlaySlider();
        }
    }

    /// <summary>Draws the category reset row.</summary>
    public void DrawResetRow(string id, int y, Action reset)
    {
        int width = _layout.List.Size.X;
        Ui.Divider(Widgets.Rect(0, y, width, 1), UiTheme.BorderInner);

        IntRect buttonRect = Widgets.Rect(0, y + 4, width, SettingsMetrics.RESET_ROW_HEIGHT - 8);
        if (Ui.ActionButton(id, buttonRect, L("ui.settings.resetSection"), ButtonVariant.Danger))
        {
            reset();
            Play();
        }
    }

    private void DrawCategoryItem(IntRect rect, string label, bool selected, bool hovering, int fontSize = UiMetrics.BODY_FONT)
    {
        Ui.LayoutDirection = LayoutDirection.None;
        Ui.Color = selected ? UiTheme.SelectionBackground : UiTheme.Transparent;
        Ui.Border = new BorderStyle
        {
            Thickness = selected || hovering ? 1 : 0,
            Color = selected ? UiTheme.Primary : UiTheme.BorderHover,
        };
        Ui.CornerCaps = default;
        Ui.Constraints = Widgets.RectConstraints(rect);
        Ui.ClipConstraints = Widgets.RectConstraints(rect);

        if (selected)
        {
            using (Ui.Element())
            {
                Ui.LayoutDirection = LayoutDirection.None;
                Ui.Color = UiTheme.Accent;
                Ui.Constraints = new Constraints
                {
                    X = new Fixed(2),
                    Y = new Fixed(6),
                    Width = new Fixed(3),
                    Height = new Fixed(Math.Max(0, rect.Size.Y - 12)),
                };
            }
        }

        Ui.Label(
            label,
            Widgets.Rect(16, 0, rect.Size.X - 22, rect.Size.Y),
            fontSize,
            selected ? UiTheme.AccentBright : hovering ? UiTheme.Text : UiTheme.TextMuted,
            Anchors.Left | Anchors.Center
        );
    }

    private void DrawEntryLabel(SettingsEntry entry, int y)
    {
        Ui.Label(
            L(entry.LabelKey),
            Widgets.Rect(0, y, _layout.LabelWidth, UiMetrics.ROW_HEIGHT),
            UiMetrics.BODY_FONT,
            entry.CanEdit ? UiTheme.Text : UiTheme.TextDim,
            Anchors.Left | Anchors.Center
        );
    }

    private IntRect ControlRect(int y)
    {
        return Widgets.Rect(
            _layout.LabelWidth + UiMetrics.ROW_GAP,
            y + (UiMetrics.ROW_HEIGHT - UiMetrics.CONTROL_HEIGHT) / 2,
            _layout.ControlWidth,
            UiMetrics.CONTROL_HEIGHT
        );
    }

    private IntRect LocalRect(IntRect rect)
    {
        return Widgets.Rect(
            rect.Left - _layout.Pane.Left,
            rect.Top - _layout.Pane.Top,
            rect.Size.X,
            rect.Size.Y
        );
    }

    private string L(string key)
    {
        return _localization.GetString(key) ?? key;
    }

    private void Play(Widgets.InterfaceSound sound = Widgets.InterfaceSound.Click)
    {
        Widgets.Interactions.Click.WithButtonAudio(_soundEffectService, sound);
    }

    private void PlaySlider()
    {
        if (Ui.Time - _lastSliderSoundTime < 0.05d)
        {
            return;
        }

        _lastSliderSoundTime = Ui.Time;
        Play(Widgets.InterfaceSound.Increase);
    }
}
