using System;
using System.Collections.Generic;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Settings;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    private static readonly bool[] BoolOptions = [false, true];

    /// <summary>Creates an on/off control and returns true when the value changes.</summary>
    public static bool SettingsToggle(
        this UIBuilder<Material> ui,
        string id,
        IntRect rect,
        bool value,
        string onLabel,
        string offLabel,
        bool enabled,
        out bool newValue
    )
    {
        return ui.SettingsChoice(
            id,
            rect,
            BoolOptions,
            value,
            option => option ? onLabel : offLabel,
            enabled,
            out newValue
        );
    }

    /// <summary>Creates a row of radio-like choices and returns true when the value changes.</summary>
    public static bool SettingsChoice<T>(
        this UIBuilder<Material> ui,
        string id,
        IntRect rect,
        IReadOnlyList<T> options,
        T value,
        Func<T, string> optionLabel,
        bool enabled,
        out T newValue
    )
    {
        newValue = value;
        var changed = false;

        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Color = SettingsTheme.ControlBackground;
            ui.Border = new BorderStyle
            {
                Thickness = 1,
                Color = enabled ? SettingsTheme.Border : SettingsTheme.BorderInner,
            };
            ui.Constraints = SettingsRectConstraints(rect);
            ui.ClipConstraints = SettingsRectConstraints(rect);

            IntRect localRect = SettingsRect(0, 0, rect.Size.X, rect.Size.Y);
            IntRect inner = SettingsInset(localRect, 1);
            int count = options.Count;
            if (count == 0)
            {
                return false;
            }

            int cellWidth = inner.Size.X / count;

            for (var i = 0; i < count; i++)
            {
                int left = inner.Left + i * cellWidth;
                int right = i == count - 1 ? inner.Right : left + cellWidth;
                var cellRect = new IntRect(left, inner.Top, right, inner.Bottom);
                bool selected = EqualityComparer<T>.Default.Equals(options[i], value);
                string cellId = $"{id}.{i}";

                if (enabled)
                {
                    using (ui.Element(cellId))
                    {
                        bool clicked = ui.Clicked();
                        bool hovering = ui.Hovering();
                        DrawChoiceCell(ui, cellRect, optionLabel(options[i]), selected, enabled: true, hovering);

                        if (clicked && !selected)
                        {
                            newValue = options[i];
                            changed = true;
                        }
                    }
                }
                else
                {
                    using (ui.Element())
                    {
                        DrawChoiceCell(ui, cellRect, optionLabel(options[i]), selected, enabled: false, hovering: false);
                    }
                }
            }

            for (var i = 1; i < count; i++)
            {
                using (ui.Element())
                {
                    int x = inner.Left + i * cellWidth;
                    ui.Color = SettingsTheme.BorderInner;
                    ui.Constraints = new Constraints
                    {
                        X = new Fixed(x),
                        Y = new Fixed(inner.Top),
                        Width = new Fixed(1),
                        Height = new Fixed(inner.Size.Y),
                    };
                }
            }
        }

        return changed;
    }

    private static void DrawChoiceCell(
        UIBuilder<Material> ui,
        IntRect rect,
        string label,
        bool selected,
        bool enabled,
        bool hovering
    )
    {
        ui.LayoutDirection = LayoutDirection.None;
        ui.Color = !enabled
            ? SettingsTheme.ControlBackground
            : selected
                ? SettingsTheme.AccentDark
                : hovering
                    ? SettingsTheme.ButtonHover
                    : SettingsTheme.ControlBackground;
        ui.Constraints = SettingsRectConstraints(rect);
        ui.ClipConstraints = SettingsRectConstraints(rect);

        using (ui.Text(label))
        {
            ui.FontOptions = new FontOptions
            {
                Size = SettingsMetrics.ControlFont,
                NoWrap = true,
            };
            ui.Color = !enabled
                ? SettingsTheme.TextDim
                : selected
                    ? SettingsTheme.AccentBright
                    : hovering
                        ? SettingsTheme.Text
                        : SettingsTheme.TextMuted;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };
        }
    }
}
