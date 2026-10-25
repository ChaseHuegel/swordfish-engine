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
    UIBuilder<Material> ui,
    ILocalization localization,
    SoundEffectService soundEffectService,
    IInputService inputService
)
{
    private readonly UIBuilder<Material> _ui = ui;
    private readonly ILocalization _localization = localization;
    private readonly SoundEffectService _soundEffectService = soundEffectService;
    private readonly IInputService _inputService = inputService;

    private SettingsLayout _layout;
    private double _lastSliderSoundTime;

    /// <summary>Sets the layout for the current frame.</summary>
    public void SetLayout(in SettingsLayout layout)
    {
        _layout = layout;
    }

    /// <summary>Draws the category tray and returns true when the selected category changes.</summary>
    public bool DrawTray(SettingsCategory[] categories, ref int categoryIndex)
    {
        IntRect tray = _layout.Tray;
        int padding = SettingsMetrics.TrayPadding;
        int width = tray.Size.X - padding * 2;

        _ui.Label(
            L("ui.settings.categories"),
            Widgets.Rect(padding, 10, width, 20),
            UiMetrics.SmallFont,
            UiTheme.TextMuted,
            Anchors.Center
        );
        _ui.Divider(Widgets.Rect(padding, SettingsMetrics.TrayHeaderHeight - 1, width, 1), UiTheme.BorderInner);

        int y = SettingsMetrics.TrayHeaderHeight + 8;
        var changed = false;

        for (var i = 0; i < categories.Length; i++)
        {
            SettingsCategory category = categories[i];
            IntRect itemRect = Widgets.Rect(padding, y, width, SettingsMetrics.TrayItemHeight);
            bool selected = i == categoryIndex;
            string itemId = $"Settings.Category.{category.Id}";
            string label = L(category.TitleKey);

            using (_ui.Element(itemId))
            {
                bool clicked = _ui.Clicked();
                bool hovering = _ui.Hovering();
                DrawCategoryItem(itemRect, label, selected, hovering);

                if (clicked && !selected)
                {
                    categoryIndex = i;
                    changed = true;
                    PlayToggle();
                }
            }

            y += SettingsMetrics.TrayItemHeight + SettingsMetrics.TrayItemGap;
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

        using (_ui.Element())
        {
            _ui.LayoutDirection = LayoutDirection.None;
            _ui.Color = UiTheme.PanelBackground;
            _ui.Border = new BorderStyle
            {
                Thickness = 1,
                Color = UiTheme.Border,
            };
            _ui.Constraints = Widgets.RectConstraints(tabs);
            _ui.ClipConstraints = Widgets.RectConstraints(tabs);

            for (var i = 0; i < categories.Length; i++)
            {
                SettingsCategory category = categories[i];
                IntRect tabRect = Widgets.Rect(i * (tabWidth + gap), 0, tabWidth, tabs.Size.Y);
                bool selected = i == categoryIndex;
                string itemId = $"Settings.Tab.{category.Id}";

                using (_ui.Element(itemId))
                {
                    bool clicked = _ui.Clicked();
                    bool hovering = _ui.Hovering();
                    DrawCategoryItem(tabRect, L(category.TitleKey), selected, hovering, UiMetrics.SmallFont);

                    if (clicked && !selected)
                    {
                        categoryIndex = i;
                        changed = true;
                        PlayToggle();
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
            SettingsMetrics.CancelButtonWidth,
            SettingsMetrics.FooterButtonHeight
        );
        IntRect applyRect = Widgets.Rect(
            footer.Right - SettingsMetrics.ApplyButtonWidth,
            footer.Top + 4,
            SettingsMetrics.ApplyButtonWidth,
            SettingsMetrics.FooterButtonHeight
        );

        cancelClicked = _ui.ActionButton(
            "Settings.Cancel",
            cancelRect,
            dirty ? L("ui.settings.cancel") : L("ui.button.return"),
            ButtonVariant.Secondary
        );
        applyClicked = _ui.ActionButton(
            "Settings.Apply",
            applyRect,
            L("ui.settings.apply"),
            ButtonVariant.Primary,
            dirty
        );

        if (dirty)
        {
            _ui.Label(
                L("ui.settings.unsaved"),
                Widgets.Rect(applyRect.Left - 110, footer.Top + 4, 100, SettingsMetrics.FooterButtonHeight),
                UiMetrics.SmallFont,
                UiTheme.Primary,
                Anchors.Right | Anchors.Center
            );
        }

        if (status != null && !dirty)
        {
            _ui.Label(
                status,
                Widgets.Rect(
                    footer.Left + SettingsMetrics.CancelButtonWidth + 20,
                    footer.Top + 4,
                    footer.Size.X - SettingsMetrics.CancelButtonWidth - SettingsMetrics.ApplyButtonWidth - 60,
                    SettingsMetrics.FooterButtonHeight
                ),
                UiMetrics.SmallFont,
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

        _ui.Label(
            L(category.TitleKey),
            Widgets.Rect(padding, 14, contentWidth, 28),
            UiMetrics.PaneTitleFont,
            UiTheme.Text,
            Anchors.Left | Anchors.Center
        );
        _ui.Divider(Widgets.Rect(padding, 62, contentWidth, 1), UiTheme.BorderInner);

        _ui.ScrollArea(
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
        _ui.Label(
            L(labelKey),
            Widgets.Rect(0, y + 8, width, 20),
            UiMetrics.SectionFont,
            UiTheme.TextMuted,
            Anchors.Left | Anchors.Center
        );
        _ui.Divider(Widgets.Rect(0, y + UiMetrics.SectionHeight - 1, width, 1), UiTheme.BorderInner);
    }

    /// <summary>Draws an on/off row.</summary>
    public void DrawToggle(ToggleEntry entry, int y)
    {
        DrawEntryLabel(entry, y);
        IntRect control = ControlRect(y);

        if (_ui.Toggle(
                entry.Id,
                control,
                entry.Value,
                L("ui.settings.on"),
                L("ui.settings.off"),
                entry.CanEdit,
                out bool newValue
            ))
        {
            entry.SetValue(newValue);
            PlayToggle();
        }
    }

    /// <summary>Draws a radio-like choice row.</summary>
    public void DrawChoice<T>(ChoiceEntry<T> entry, int y)
    {
        DrawEntryLabel(entry, y);
        IntRect control = ControlRect(y);

        if (_ui.SegmentedControl(
                entry.Id,
                control,
                entry.Options,
                entry.Value,
                entry.GetOptionLabel,
                entry.CanEdit,
                out T newValue
            ))
        {
            entry.SetValue(newValue);
            PlayToggle();
        }
    }

    /// <summary>Draws a discrete stepper row.</summary>
    public void DrawStepper<T>(StepperEntry<T> entry, int y)
    {
        DrawEntryLabel(entry, y);
        IntRect control = ControlRect(y);
        int index = entry.IndexOf(entry.Value);

        if (_ui.Stepper(
                entry.Id,
                control,
                entry.Options,
                index,
                entry.Format,
                entry.CanEdit,
                out int newIndex
            ))
        {
            if (newIndex >= 0 && newIndex < entry.Options.Count)
            {
                entry.SetValue(entry.Options[newIndex]);
                if (newIndex > index)
                {
                    PlayIncrease();
                }
                else
                {
                    PlayDecrease();
                }
            }
        }
    }

    /// <summary>Draws a slider row.</summary>
    public void DrawSlider(SliderEntry entry, int y)
    {
        DrawEntryLabel(entry, y);
        IntRect control = ControlRect(y);

        if (_ui.Slider(
                entry.Id,
                control,
                entry.Value,
                entry.Min,
                entry.Max,
                entry.Step,
                entry.Format,
                entry.CanEdit,
                out float newValue
            ))
        {
            entry.SetValue(newValue);
            PlaySlider();
        }
    }

    /// <summary>Draws the category reset row.</summary>
    public void DrawResetRow(string id, int y, Action reset)
    {
        int width = _layout.List.Size.X;
        _ui.Divider(Widgets.Rect(0, y, width, 1), UiTheme.BorderInner);

        IntRect buttonRect = Widgets.Rect(0, y + 4, width, SettingsMetrics.ResetRowHeight - 8);
        if (_ui.ActionButton(id, buttonRect, L("ui.settings.resetSection"), ButtonVariant.Danger))
        {
            reset();
            PlayButton();
        }
    }

    private void DrawCategoryItem(IntRect rect, string label, bool selected, bool hovering, int fontSize = UiMetrics.BodyFont)
    {
        _ui.LayoutDirection = LayoutDirection.None;
        _ui.Color = selected ? UiTheme.SelectionBackground : UiTheme.Transparent;
        _ui.Border = new BorderStyle
        {
            Thickness = selected || hovering ? 1 : 0,
            Color = selected ? UiTheme.Primary : UiTheme.BorderHover,
        };
        _ui.CornerCaps = default;
        _ui.Constraints = Widgets.RectConstraints(rect);
        _ui.ClipConstraints = Widgets.RectConstraints(rect);

        if (selected)
        {
            using (_ui.Element())
            {
                _ui.LayoutDirection = LayoutDirection.None;
                _ui.Color = UiTheme.Accent;
                _ui.Constraints = new Constraints
                {
                    X = new Fixed(2),
                    Y = new Fixed(6),
                    Width = new Fixed(3),
                    Height = new Fixed(Math.Max(0, rect.Size.Y - 12)),
                };
            }
        }

        _ui.Label(
            label,
            Widgets.Rect(16, 0, rect.Size.X - 22, rect.Size.Y),
            fontSize,
            selected ? UiTheme.AccentBright : hovering ? UiTheme.Text : UiTheme.TextMuted,
            Anchors.Left | Anchors.Center
        );
    }

    private void DrawEntryLabel(SettingsEntry entry, int y)
    {
        _ui.Label(
            L(entry.LabelKey),
            Widgets.Rect(0, y, _layout.LabelWidth, UiMetrics.RowHeight),
            UiMetrics.BodyFont,
            entry.CanEdit ? UiTheme.Text : UiTheme.TextDim,
            Anchors.Left | Anchors.Center
        );
    }

    private IntRect ControlRect(int y)
    {
        return Widgets.Rect(
            _layout.LabelWidth + UiMetrics.RowGap,
            y + (UiMetrics.RowHeight - UiMetrics.ControlHeight) / 2,
            _layout.ControlWidth,
            UiMetrics.ControlHeight
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

    private void PlayToggle()
    {
        Widgets.Interactions.Click.WithButtonToggleAudio(_soundEffectService);
    }

    private void PlayIncrease()
    {
        Widgets.Interactions.Click.WithButtonIncreaseAudio(_soundEffectService);
    }

    private void PlayDecrease()
    {
        Widgets.Interactions.Click.WithButtonDecreaseAudio(_soundEffectService);
    }

    private void PlayButton()
    {
        Widgets.Interactions.Click.WithButtonAudio(_soundEffectService);
    }

    private void PlaySlider()
    {
        if (_ui.Time - _lastSliderSoundTime < 0.05d)
        {
            return;
        }

        _lastSliderSoundTime = _ui.Time;
        PlayIncrease();
    }
}
