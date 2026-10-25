using System;
using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    /// <summary>Creates a fixed-position action button and returns true when clicked.</summary>
    public static bool ActionButton(
        this UIBuilder<Material> ui,
        string id,
        IntRect rect,
        string text,
        ButtonVariant variant,
        bool enabled = true,
        string? fontId = null,
        SoundEffectService? soundEffectService = null
    )
    {
        return ui.ActionButton(id, text, new ActionButtonOptions(
            Variant: variant,
            Enabled: enabled,
            Rect: rect,
            Font: fontId == null
                ? null
                : new FontOptions
                {
                    ID = fontId,
                    Size = UiMetrics.IconFont,
                    NoWrap = true,
                },
            SoundEffectService: soundEffectService
        ));
    }

    /// <summary>Creates an action button and returns true when clicked.</summary>
    public static bool ActionButton(
        this UIBuilder<Material> ui,
        string id,
        string text,
        ActionButtonOptions options
    )
    {
        return ui.ActionButton(id, text, options, out _);
    }

    /// <summary>Creates an action button and reports hover state.</summary>
    public static bool ActionButton(
        this UIBuilder<Material> ui,
        string id,
        string text,
        ActionButtonOptions options,
        out bool hovering
    )
    {
        bool clicked;

        if (options.Enabled)
        {
            using (ui.Element(id))
            {
                clicked = ui.Clicked();
                hovering = ui.Hovering();
                DrawActionButton(ui, text, options, enabled: true, hovering, clicked);
            }
        }
        else
        {
            using (ui.Element())
            {
                DrawActionButton(ui, text, options, enabled: false, hovering: false, clicked: false);
            }

            clicked = false;
            hovering = false;
        }

        if (clicked && options.SoundEffectService != null)
        {
            Interactions.Click.WithButtonAudio(options.SoundEffectService);
        }

        return clicked;
    }

    private static void DrawActionButton(
        UIBuilder<Material> ui,
        string text,
        ActionButtonOptions options,
        bool enabled,
        bool hovering,
        bool clicked
    )
    {
        ControlStyle style = UiThemeResolver.ResolveButton(options.Variant, enabled, hovering, clicked);

        if (options.DoubleBorder && enabled)
        {
            if (options.Variant == ButtonVariant.Secondary && hovering)
            {
                style = style with { Border = UiTheme.Accent };
            }
            else if (options.Variant == ButtonVariant.Primary)
            {
                style = style with { Text = hovering || clicked ? UiTheme.Text : UiTheme.AccentBright };
            }
        }

        if (options.Selected && enabled)
        {
            style = new ControlStyle(
                UiTheme.SelectionBackground,
                UiTheme.Accent,
                UiMetrics.PanelBorderThickness,
                new CornerCapStyle
                {
                    Length = UiMetrics.SelectionCapLength,
                    Thickness = UiMetrics.SelectionCapThickness,
                    Color = UiTheme.Accent,
                },
                UiTheme.AccentBright
            );
        }

        ui.LayoutDirection = LayoutDirection.None;
        ui.Color = style.Fill;
        ui.Border = new BorderStyle
        {
            Thickness = style.BorderThickness,
            Color = style.Border,
        };
        ui.CornerCaps = style.Caps;
        ui.Padding = options.DoubleBorder
            ? new Padding(0)
            : new Padding(options.PaddingX, options.PaddingY, options.PaddingX, options.PaddingY);

        if (options.Rect is { } rect)
        {
            ui.Constraints = RectConstraints(rect);
            ui.ClipConstraints = RectConstraints(rect);
        }
        else
        {
            var constraints = new Constraints
            {
                Anchors = options.Anchors,
            };

            if (options.MinWidth > 0)
            {
                constraints.Width = new Fixed(options.MinWidth);
            }

            if (options.Height > 0)
            {
                constraints.Height = new Fixed(options.Height);
            }

            ui.Constraints = constraints;
        }

        if (options.Rect is { } buttonRect && options.Variant == ButtonVariant.Primary && enabled)
        {
            DrawInnerBorder(ui, buttonRect, UiTheme.PrimaryDark);
        }
        else if (options.DoubleBorder && enabled && style.BorderThickness > 0)
        {
            int width = options.Rect?.Size.X ?? options.MinWidth;
            int height = options.Rect?.Size.Y ?? options.Height;
            if (width > 0 && height > 0)
            {
                Vector4 color = options.Variant switch
                {
                    ButtonVariant.Primary => UiTheme.PrimaryDark,
                    ButtonVariant.Danger => UiTheme.DangerDark,
                    _ => UiTheme.BorderInner,
                };
                DrawInnerBorder(ui, Rect(0, 0, width, height), color);
            }
        }

        FontOptions font = options.Font ?? new FontOptions
        {
            Size = options.Rect != null ? UiMetrics.ButtonFont : UiMetrics.MenuBodyFont,
            NoWrap = true,
        };
        font.ID ??= UiTheme.FontUI;
        font.NoWrap = true;

        using (ui.Text(text))
        {
            ui.FontOptions = font;
            ui.Color = style.Text;
            ui.Constraints = new Constraints
            {
                Anchors = options.ContentAnchors,
            };
        }
    }

    private static void DrawInnerBorder(UIBuilder<Material> ui, IntRect rect, Vector4 color)
    {
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Color = UiTheme.Transparent;
            ui.Border = new BorderStyle
            {
                Thickness = UiMetrics.PanelBorderThickness,
                Color = color,
            };
            IntRect inner = Rect(2, 2, Math.Max(0, rect.Size.X - 4), Math.Max(0, rect.Size.Y - 4));
            ui.Constraints = RectConstraints(inner);
            ui.ClipConstraints = RectConstraints(inner);
        }
    }

    /// <summary>Options for an action button.</summary>
    public readonly record struct ActionButtonOptions(
        ButtonVariant Variant = ButtonVariant.Secondary,
        bool Enabled = true,
        int MinWidth = 0,
        int Height = 0,
        int PaddingX = 0,
        int PaddingY = 0,
        FontOptions? Font = null,
        Anchors ContentAnchors = Anchors.Center,
        Anchors Anchors = default,
        SoundEffectService? SoundEffectService = null,
        IntRect? Rect = null,
        bool Selected = false,
        bool DoubleBorder = false
    )
    {
        /// <summary>Creates a full-size menu button.</summary>
        public static ActionButtonOptions Menu(
            ButtonVariant variant,
            SoundEffectService soundEffectService,
            FontOptions? font = null
        )
        {
            return new ActionButtonOptions(
                Variant: variant,
                MinWidth: UiMetrics.MenuButtonMinWidth,
                Height: UiMetrics.MenuButtonHeight,
                PaddingX: UiMetrics.MenuButtonPaddingX,
                PaddingY: UiMetrics.MenuButtonPaddingY,
                Font: font ?? new FontOptions { ID = UiTheme.FontUI, Size = UiMetrics.MenuBodyFont, NoWrap = true },
                ContentAnchors: Anchors.Center,
                SoundEffectService: soundEffectService,
                DoubleBorder: true
            );
        }

        /// <summary>Creates a compact control button.</summary>
        public static ActionButtonOptions Compact(
            ButtonVariant variant,
            SoundEffectService soundEffectService,
            FontOptions? font = null
        )
        {
            return new ActionButtonOptions(
                Variant: variant,
                Height: UiMetrics.CompactButtonHeight,
                PaddingX: UiMetrics.CompactButtonPaddingX,
                PaddingY: UiMetrics.CompactButtonPaddingY,
                Font: font ?? new FontOptions { ID = UiTheme.FontUI, Size = UiMetrics.MenuSmallFont, NoWrap = true },
                ContentAnchors: Anchors.Center,
                SoundEffectService: soundEffectService
            );
        }

        /// <summary>Creates a full-width list row button.</summary>
        public static ActionButtonOptions ListRow(
            ButtonVariant variant,
            SoundEffectService soundEffectService,
            FontOptions? font = null
        )
        {
            return new ActionButtonOptions(
                Variant: variant,
                MinWidth: UiMetrics.ListRowMinWidth,
                Height: UiMetrics.ListRowHeight,
                PaddingX: UiMetrics.ListRowPaddingX,
                Font: font ?? new FontOptions { ID = UiTheme.FontUI, Size = UiMetrics.MenuBodyFont, NoWrap = true },
                ContentAnchors: Anchors.Left | Anchors.Center,
                SoundEffectService: soundEffectService
            );
        }

        /// <summary>Creates a square icon button.</summary>
        public static ActionButtonOptions Icon(
            ButtonVariant variant,
            SoundEffectService soundEffectService,
            int size = UiMetrics.IconButtonSize,
            int fontSize = UiMetrics.LargeIconFont,
            string? fontId = null
        )
        {
            return new ActionButtonOptions(
                Variant: variant,
                MinWidth: size,
                Height: size,
                Font: new FontOptions { ID = fontId ?? UiTheme.FontIcons, Size = fontSize, NoWrap = true },
                ContentAnchors: Anchors.Center,
                SoundEffectService: soundEffectService
            );
        }
    }
}
