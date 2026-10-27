using System;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;

namespace WaywardBeyond.Client.UI.Layers.Menus;

/// <summary>Responsive geometry for one menu page at one window size.</summary>
internal readonly record struct MenuLayout(
    int PageWidth,
    int PageHeight,
    bool UseTabs,
    float LeftRatio
)
{
    public const int PAGE_GAP = 12;
    public const int PANEL_GAP = 20;
    public const int FOOTER_HEIGHT = 52;
    public const int TABS_HEIGHT = 40;
    public const int FOOTER_BUTTON_HEIGHT = 40;
    public const int RETURN_BUTTON_WIDTH = 170;
    public const int PRIMARY_BUTTON_WIDTH = 220;
    public const int MIN_PAGE_WIDTH = 480;
    public const int MIN_PAGE_HEIGHT = 360;
    public const int MAX_PAGE_HEIGHT = 560;
    public const int NARROW_BREAKPOINT = 900;

    /// <summary>Computes the menu geometry for a window size.</summary>
    public static MenuLayout Compute(
        int windowWidth,
        int windowHeight,
        int maxPageWidth,
        bool twoColumn,
        float leftRatio = 0.4f,
        int maxPageHeight = MAX_PAGE_HEIGHT
    )
    {
        int pageWidth = Math.Max(MIN_PAGE_WIDTH, Math.Min(windowWidth - 120, maxPageWidth));
        int pageHeight = Math.Max(MIN_PAGE_HEIGHT, Math.Min(windowHeight - 240, maxPageHeight));
        bool useTabs = twoColumn && pageWidth < NARROW_BREAKPOINT;

        return new MenuLayout(
            pageWidth,
            pageHeight,
            useTabs,
            leftRatio
        );
    }
}

/// <summary>Layout helpers shared by the menu pages.</summary>
internal static class MenuLayoutExtensions
{
    /// <summary>Opens a centered page for the layout.</summary>
    public static UIBuilder<Material>.Scope Page(
        this UIBuilder<Material> ui,
        in MenuLayout layout,
        string? id = null
    )
    {
        return ui.Page(layout.PageWidth, layout.PageHeight, id);
    }

    /// <summary>Adds the shared footer size to an action button.</summary>
    public static Widgets.ActionButtonOptions FooterButton(
        this Widgets.ActionButtonOptions options,
        int width
    )
    {
        return options with
        {
            Layout = new Constraints
            {
                Width = new Fixed(width),
                Height = new Fixed(MenuLayout.FOOTER_BUTTON_HEIGHT),
                Anchors = Anchors.Center,
            },
        };
    }
}
