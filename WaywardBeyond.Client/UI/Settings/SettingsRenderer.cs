using System;
using System.Collections.Generic;
using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using WaywardBeyond.Client.Services;

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

    /// <summary>Draws the page title.</summary>
    public void DrawPageTitle()
    {
        DrawText(
            L("ui.menu.settings"),
            _layout.Title,
            SettingsMetrics.PageTitleFont,
            SettingsTheme.Text,
            Anchors.Center
        );
    }

    /// <summary>Draws the category tray and returns true when the selected category changes.</summary>
    public bool DrawTray(SettingsCategory[] categories, ref int categoryIndex)
    {
        IntRect tray = _layout.Tray;
        int padding = SettingsMetrics.TrayPadding;
        int width = tray.Size.X - padding * 2;

        DrawText(
            L("ui.settings.categories"),
            Widgets.SettingsRect(padding, 10, width, 20),
            SettingsMetrics.SmallFont,
            SettingsTheme.TextMuted,
            Anchors.Center
        );
        DrawLine(Widgets.SettingsRect(padding, SettingsMetrics.TrayHeaderHeight - 1, width, 1), SettingsTheme.BorderInner);

        int y = SettingsMetrics.TrayHeaderHeight + 8;
        var changed = false;
        bool useShortLabels = tray.Size.X < 280;

        for (var i = 0; i < categories.Length; i++)
        {
            SettingsCategory category = categories[i];
            IntRect itemRect = Widgets.SettingsRect(padding, y, width, SettingsMetrics.TrayItemHeight);
            bool selected = i == categoryIndex;
            string itemId = $"Settings.Category.{category.Id}";
            string label = L(useShortLabels ? category.ShortTitleKey : category.TitleKey);

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

        using (_ui.Element())
        {
            _ui.Color = SettingsTheme.PanelBackground;
            _ui.Border = new BorderStyle
            {
                Thickness = 1,
                Color = SettingsTheme.Border,
            };
            _ui.Constraints = Widgets.SettingsRectConstraints(tabs);
            _ui.ClipConstraints = Widgets.SettingsRectConstraints(tabs);
        }

        var changed = false;

        for (var i = 0; i < categories.Length; i++)
        {
            SettingsCategory category = categories[i];
            IntRect tabRect = Widgets.SettingsRect(tabs.Left + i * (tabWidth + gap), tabs.Top, tabWidth, tabs.Size.Y);
            bool selected = i == categoryIndex;
            string itemId = $"Settings.Tab.{category.Id}";

            using (_ui.Element(itemId))
            {
                bool clicked = _ui.Clicked();
                bool hovering = _ui.Hovering();
                DrawCategoryItem(tabRect, L(category.ShortTitleKey), selected, hovering, SettingsMetrics.SmallFont);

                if (clicked && !selected)
                {
                    categoryIndex = i;
                    changed = true;
                    PlayToggle();
                }
            }
        }

        return changed;
    }

    /// <summary>Draws the page footer and reports the clicked action.</summary>
    public void DrawFooter(bool dirty, string? status, out bool cancelClicked, out bool applyClicked)
    {
        IntRect footer = _layout.Footer;
        IntRect cancelRect = Widgets.SettingsRect(
            footer.Left,
            footer.Top + 4,
            SettingsMetrics.CancelButtonWidth,
            SettingsMetrics.FooterButtonHeight
        );
        IntRect applyRect = Widgets.SettingsRect(
            footer.Right - SettingsMetrics.ApplyButtonWidth,
            footer.Top + 4,
            SettingsMetrics.ApplyButtonWidth,
            SettingsMetrics.FooterButtonHeight
        );

        cancelClicked = _ui.SettingsButton(
            "Settings.Cancel",
            cancelRect,
            L("ui.settings.cancel"),
            SettingsButtonVariant.Secondary
        );
        applyClicked = _ui.SettingsButton(
            "Settings.Apply",
            applyRect,
            L("ui.settings.apply"),
            SettingsButtonVariant.Primary,
            dirty
        );

        if (dirty)
        {
            DrawText(
                L("ui.settings.unsaved"),
                Widgets.SettingsRect(applyRect.Left - 110, footer.Top + 4, 100, SettingsMetrics.FooterButtonHeight),
                SettingsMetrics.SmallFont,
                SettingsTheme.Primary,
                Anchors.Right | Anchors.Center
            );
        }

        if (status != null && !dirty)
        {
            DrawText(
                status,
                Widgets.SettingsRect(
                    footer.Left + SettingsMetrics.CancelButtonWidth + 20,
                    footer.Top + 4,
                    footer.Size.X - SettingsMetrics.CancelButtonWidth - SettingsMetrics.ApplyButtonWidth - 60,
                    SettingsMetrics.FooterButtonHeight
                ),
                SettingsMetrics.SmallFont,
                SettingsTheme.Success,
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

        DrawText(
            L(category.TitleKey),
            Widgets.SettingsRect(padding, 14, contentWidth, 28),
            SettingsMetrics.PaneTitleFont,
            SettingsTheme.Text,
            Anchors.Left | Anchors.Center
        );
        DrawText(
            L(category.DescriptionKey),
            Widgets.SettingsRect(padding, 42, contentWidth, 18),
            SettingsMetrics.SmallFont,
            SettingsTheme.TextMuted,
            Anchors.Left | Anchors.Center
        );
        DrawLine(Widgets.SettingsRect(padding, 62, contentWidth, 1), SettingsTheme.BorderInner);

        int viewportHeight = _layout.List.Size.Y;
        int maxScroll = Math.Max(0, category.ContentHeight - viewportHeight);
        float wheel = _inputService.GetMouseScroll();
        if (wheel != 0f)
        {
            scrollY -= (int)MathF.Round(wheel * SettingsMetrics.ScrollStep);
        }

        scrollY = Math.Clamp(scrollY, 0, maxScroll);

        IntRect list = LocalRect(_layout.List);
        using (_ui.Element())
        {
            _ui.LayoutDirection = LayoutDirection.None;
            _ui.Color = SettingsTheme.Transparent;
            _ui.Constraints = Widgets.SettingsRectConstraints(list);
            _ui.ClipConstraints = new Constraints
            {
                Width = new Fixed(list.Size.X),
                Height = new Fixed(list.Size.Y),
            };
            _ui.ScrollY = -scrollY;

            var rowY = 0;
            for (var i = 0; i < category.Rows.Length; i++)
            {
                SettingsRow row = category.Rows[i];
                row.Render(this, rowY);
                rowY += row.Height;
            }
        }

        IntRect scrollbar = LocalRect(_layout.Scrollbar);
        _ui.SettingsScrollbar(scrollbar, category.ContentHeight, viewportHeight, scrollY);
    }

    /// <summary>Draws a section header row.</summary>
    public void DrawSection(string labelKey, int y)
    {
        int width = _layout.List.Size.X;
        DrawText(
            L(labelKey),
            Widgets.SettingsRect(0, y + 8, width, 20),
            SettingsMetrics.SectionFont,
            SettingsTheme.TextMuted,
            Anchors.Left | Anchors.Center
        );
        DrawLine(Widgets.SettingsRect(0, y + SettingsMetrics.SectionHeight - 1, width, 1), SettingsTheme.BorderInner);
    }

    /// <summary>Draws an on/off row.</summary>
    public void DrawToggle(ToggleEntry entry, int y)
    {
        DrawEntryLabel(entry, y);
        IntRect control = ControlRect(y);

        if (_ui.SettingsToggle(
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

        if (_ui.SettingsChoice(
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

        if (_ui.SettingsStepper(
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

        if (_ui.SettingsSlider(
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
        DrawLine(Widgets.SettingsRect(0, y, width, 1), SettingsTheme.BorderInner);

        IntRect buttonRect = Widgets.SettingsRect(0, y + 4, width, SettingsMetrics.ResetRowHeight - 8);
        if (_ui.SettingsButton(id, buttonRect, L("ui.settings.resetSection"), SettingsButtonVariant.Subtle))
        {
            reset();
            PlayButton();
        }
    }

    private void DrawCategoryItem(IntRect rect, string label, bool selected, bool hovering, int fontSize = SettingsMetrics.BodyFont)
    {
        _ui.LayoutDirection = LayoutDirection.None;
        _ui.Color = selected ? SettingsTheme.SelectionBackground : SettingsTheme.Transparent;
        _ui.Border = new BorderStyle
        {
            Thickness = selected || hovering ? 1 : 0,
            Color = selected ? SettingsTheme.Accent : SettingsTheme.BorderHover,
        };
        _ui.CornerCaps = selected
            ? new CornerCapStyle
            {
                Length = SettingsMetrics.SelectionCapLength,
                Thickness = SettingsMetrics.SelectionCapThickness,
                Color = SettingsTheme.Accent,
            }
            : default;
        _ui.Constraints = Widgets.SettingsRectConstraints(rect);
        _ui.ClipConstraints = Widgets.SettingsRectConstraints(rect);

        DrawText(
            selected ? "[*]" : "[ ]",
            Widgets.SettingsRect(10, 0, 24, rect.Size.Y),
            fontSize,
            selected ? SettingsTheme.AccentBright : SettingsTheme.TextDim,
            Anchors.Left | Anchors.Center
        );
        DrawText(
            label,
            Widgets.SettingsRect(36, 0, rect.Size.X - 46, rect.Size.Y),
            fontSize,
            selected ? SettingsTheme.AccentBright : hovering ? SettingsTheme.Text : SettingsTheme.TextMuted,
            Anchors.Left | Anchors.Center
        );
    }

    private void DrawEntryLabel(SettingsEntry entry, int y)
    {
        DrawText(
            L(entry.LabelKey),
            Widgets.SettingsRect(0, y, _layout.LabelWidth, SettingsMetrics.RowHeight),
            SettingsMetrics.BodyFont,
            entry.CanEdit ? SettingsTheme.Text : SettingsTheme.TextDim,
            Anchors.Left | Anchors.Center
        );
    }

    private IntRect ControlRect(int y)
    {
        return Widgets.SettingsRect(
            _layout.LabelWidth + SettingsMetrics.RowGap,
            y + (SettingsMetrics.RowHeight - SettingsMetrics.ControlHeight) / 2,
            _layout.ControlWidth,
            SettingsMetrics.ControlHeight
        );
    }

    private IntRect LocalRect(IntRect rect)
    {
        return Widgets.SettingsRect(
            rect.Left - _layout.Pane.Left,
            rect.Top - _layout.Pane.Top,
            rect.Size.X,
            rect.Size.Y
        );
    }

    private void DrawText(string text, IntRect rect, int fontSize, Vector4 color, Anchors anchors)
    {
        using (_ui.Element())
        {
            _ui.LayoutDirection = LayoutDirection.None;
            _ui.Color = SettingsTheme.Transparent;
            _ui.Constraints = Widgets.SettingsRectConstraints(rect);
            _ui.ClipConstraints = Widgets.SettingsRectConstraints(rect);

            using (_ui.Text(text))
            {
                _ui.FontOptions = new FontOptions
                {
                    Size = fontSize,
                    NoWrap = true,
                };
                _ui.Color = color;
                _ui.Constraints = new Constraints
                {
                    Anchors = anchors,
                };
            }
        }
    }

    private void DrawLine(IntRect rect, Vector4 color)
    {
        using (_ui.Element())
        {
            _ui.Color = color;
            _ui.Constraints = Widgets.SettingsRectConstraints(rect);
        }
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
