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

    /// <summary>Draws the scrolling rows of the active category.</summary>
    public void DrawRows(SettingsCategory category, ref int scrollY)
    {
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
                L("ui.settings.value.on"),
                L("ui.settings.value.off"),
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
        if (Ui.ActionButton(id, L("ui.settings.action.reset"), options))
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
