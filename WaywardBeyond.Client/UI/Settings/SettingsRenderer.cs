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
        Ui.Label(
            L("ui.settings.categories"),
            UiMetrics.SMALL_FONT,
            UiTheme.TextMuted,
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(20),
            },
            HorizontalAlignment.Center
        );
        Ui.Spacer(height: 4);
        Ui.Divider(
            UiTheme.BorderInner,
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(1),
            }
        );
        Ui.Spacer(height: 8);

        var changed = false;

        for (var i = 0; i < categories.Length; i++)
        {
            SettingsCategory category = categories[i];
            bool selected = i == categoryIndex;
            string itemId = $"Settings.Category.{category.Id}";
            string label = L(category.TitleKey);

            using (Ui.Element(itemId))
            {
                bool clicked = Ui.Clicked();
                bool hovering = Ui.Hovering();
                DrawCategoryItem(label, selected, hovering);

                if (clicked && !selected)
                {
                    categoryIndex = i;
                    changed = true;
                    Play(Widgets.InterfaceSound.Toggle);
                }
            }

            Ui.Spacer(height: SettingsMetrics.TRAY_ITEM_GAP);
        }

        return changed;
    }

    /// <summary>Draws the horizontal category strip and returns true when the selected category changes.</summary>
    public bool DrawTabs(SettingsCategory[] categories, ref int categoryIndex)
    {
        var changed = false;

        using (Ui.Row(
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(SettingsLayout.TABS_HEIGHT),
            },
            spacing: 8
        ))
        {
            for (var i = 0; i < categories.Length; i++)
            {
                SettingsCategory category = categories[i];
                bool selected = i == categoryIndex;
                string itemId = $"Settings.Tab.{category.Id}";
                var constraints = new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                };

                if (Ui.Tab(itemId, L(category.TitleKey), selected, constraints) && !selected)
                {
                    categoryIndex = i;
                    changed = true;
                    Play(Widgets.InterfaceSound.Toggle);
                }
            }
        }

        return changed;
    }

    /// <summary>Draws the page footer and reports the clicked action.</summary>
    public void DrawFooter(bool dirty, string? status, out bool cancelClicked, out bool applyClicked)
    {
        using (Ui.Row(
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(SettingsLayout.FOOTER_HEIGHT),
            },
            spacing: 12
        ))
        {
            Widgets.ActionButtonOptions cancelOptions = Widgets.ActionButtonOptions.Menu(
                ButtonVariant.Secondary,
                soundEffectService: null
            ) with
            {
                Layout = new Constraints
                {
                    Width = new Fixed(SettingsMetrics.CANCEL_BUTTON_WIDTH),
                    Height = new Fixed(SettingsMetrics.FOOTER_BUTTON_HEIGHT),
                    Anchors = Anchors.Center,
                },
            };
            cancelClicked = Ui.ActionButton(
                "Settings.Cancel",
                dirty ? L("ui.settings.cancel") : L("ui.button.return"),
                cancelOptions
            );

            string? message = dirty ? L("ui.settings.unsaved") : status;
            if (message != null)
            {
                Ui.Label(
                    message,
                    UiMetrics.SMALL_FONT,
                    dirty ? UiTheme.Primary : UiTheme.Success,
                    new Constraints
                    {
                        Width = new Fill(),
                        Height = new Fill(),
                    },
                    HorizontalAlignment.Center
                );
            }
            else
            {
                Ui.Filler();
            }

            Widgets.ActionButtonOptions applyOptions = Widgets.ActionButtonOptions.Menu(
                ButtonVariant.Primary,
                soundEffectService: null
            ) with
            {
                Enabled = dirty,
                Layout = new Constraints
                {
                    Width = new Fixed(SettingsMetrics.APPLY_BUTTON_WIDTH),
                    Height = new Fixed(SettingsMetrics.FOOTER_BUTTON_HEIGHT),
                    Anchors = Anchors.Center,
                },
            };
            applyClicked = Ui.ActionButton(
                "Settings.Apply",
                L("ui.settings.apply"),
                applyOptions
            );
        }
    }

    /// <summary>Draws the active category pane and updates the scroll offset.</summary>
    public void DrawPane(SettingsCategory category, ref int scrollY)
    {
        Ui.Label(
            L(category.TitleKey),
            UiMetrics.PANE_TITLE_FONT,
            UiTheme.Text,
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(28),
            }
        );
        Ui.Divider(
            UiTheme.BorderInner,
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(1),
            }
        );
        Ui.Spacer(height: 8);

        Ui.ScrollArea(
            "Settings.Rows",
            category.ContentHeight,
            ref scrollY,
            _inputService,
            _ =>
            {
                for (var i = 0; i < category.Rows.Length; i++)
                {
                    category.Rows[i].Render(this);
                }
            },
            SettingsLayout.SCROLLBAR_WIDTH,
            SettingsLayout.SCROLLBAR_GAP
        );
    }

    /// <summary>Draws a section header row.</summary>
    public void DrawSection(string labelKey)
    {
        Ui.Label(
            L(labelKey),
            UiMetrics.SECTION_FONT,
            UiTheme.TextMuted,
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(UiMetrics.SECTION_HEIGHT - 1),
            }
        );
        Ui.Divider(
            UiTheme.BorderInner,
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(1),
            }
        );
    }

    /// <summary>Draws an on/off row.</summary>
    public void DrawToggle(ToggleEntry entry)
    {
        using (DrawEntryRow(entry))
        {
            ControlResult<bool> result = Ui.Toggle(
                entry.Id,
                ControlConstraints(),
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
    }

    /// <summary>Draws a radio-like choice row.</summary>
    public void DrawChoice<T>(ChoiceEntry<T> entry)
    {
        using (DrawEntryRow(entry))
        {
            ControlResult<T> result = Ui.SegmentedControl(
                entry.Id,
                ControlConstraints(),
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
    }

    /// <summary>Draws a discrete stepper row.</summary>
    public void DrawStepper<T>(StepperEntry<T> entry)
    {
        using (DrawEntryRow(entry))
        {
            int index = entry.IndexOf(entry.Value);
            ControlResult<int> result = Ui.Stepper(
                entry.Id,
                ControlConstraints(),
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
    }

    /// <summary>Draws a slider row.</summary>
    public void DrawSlider(SliderEntry entry)
    {
        using (DrawEntryRow(entry))
        {
            ControlResult<float> result = Ui.Slider(
                entry.Id,
                ControlConstraints(),
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
    }

    /// <summary>Draws the category reset row.</summary>
    public void DrawResetRow(string id, Action reset)
    {
        Ui.Divider(
            UiTheme.BorderInner,
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(1),
            }
        );
        Ui.Spacer(height: 4);

        Widgets.ActionButtonOptions options = new(
            Variant: ButtonVariant.Danger,
            Layout: new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(SettingsMetrics.RESET_ROW_HEIGHT - 8),
            }
        );
        if (Ui.ActionButton(id, L("ui.settings.resetSection"), options))
        {
            reset();
            Play();
        }

        Ui.Spacer(height: 3);
    }

    private UIBuilder<Material>.Scope DrawEntryRow(SettingsEntry entry)
    {
        UIBuilder<Material>.Scope row = Ui.Row(
            new Constraints
            {
                Width = new Fill(),
                Height = new Fixed(UiMetrics.ROW_HEIGHT),
            },
            spacing: UiMetrics.ROW_GAP
        );
        Ui.Label(
            L(entry.LabelKey),
            UiMetrics.BODY_FONT,
            entry.CanEdit ? UiTheme.Text : UiTheme.TextDim,
            new Constraints
            {
                Width = new Fill(),
                Height = new Fill(),
            }
        );
        return row;
    }

    private void DrawCategoryItem(string label, bool selected, bool hovering)
    {
        Ui.LayoutDirection = LayoutDirection.None;
        Ui.Color = selected ? UiTheme.SelectionBackground : UiTheme.Transparent;
        Ui.Border = new BorderStyle
        {
            Thickness = selected || hovering ? 1 : 0,
            Color = selected ? UiTheme.Primary : UiTheme.BorderHover,
        };
        Ui.CornerCaps = default;
        Ui.Constraints = new Constraints
        {
            Width = new Fill(),
            Height = new Fixed(SettingsMetrics.TRAY_ITEM_HEIGHT),
        };
        Ui.ClipConstraints = Widgets.ClipToBounds();

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
                    Height = new Fixed(SettingsMetrics.TRAY_ITEM_HEIGHT - 12),
                };
            }
        }

        using (Ui.Element())
        {
            Ui.LayoutDirection = LayoutDirection.None;
            Ui.Padding = new Padding(left: 16, right: 6, top: 0, bottom: 0);
            Ui.Constraints = new Constraints
            {
                Width = new Fill(),
                Height = new Fill(),
            };
            Ui.Label(
                label,
                UiMetrics.BODY_FONT,
                selected ? UiTheme.AccentBright : hovering ? UiTheme.Text : UiTheme.TextMuted,
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                }
            );
        }
    }

    private Constraints ControlConstraints()
    {
        return new Constraints
        {
            Width = new Fixed(_layout.ControlWidth),
            Height = new Fixed(UiMetrics.CONTROL_HEIGHT),
            Anchors = Anchors.Center,
        };
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
