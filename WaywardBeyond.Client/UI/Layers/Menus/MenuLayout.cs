using System;
using Reef;

namespace WaywardBeyond.Client.UI.Layers.Menus;

/// <summary>Fixed-metric geometry for one menu page at one window size.</summary>
internal readonly record struct MenuLayout(
    int PageWidth,
    int PageHeight,
    bool UseTabs,
    IntRect Title,
    IntRect Left,
    IntRect Right,
    IntRect Tabs,
    IntRect Footer,
    IntRect ReturnButton,
    IntRect PrimaryButton
)
{
    private const int PAGE_TITLE_HEIGHT = 36;
    private const int PAGE_GAP = 12;
    private const int PANEL_GAP = 20;
    private const int FOOTER_HEIGHT = 52;
    private const int TABS_HEIGHT = 40;
    private const int MIN_PAGE_WIDTH = 480;
    private const int MIN_PAGE_HEIGHT = 360;
    private const int MAX_PAGE_HEIGHT = 720;
    private const int NARROW_BREAKPOINT = 900;
    private const int MIN_LEFT_WIDTH = 280;
    private const int MIN_RIGHT_WIDTH = 320;
    private const int RETURN_WIDTH = 170;
    private const int PRIMARY_WIDTH = 220;

    /// <summary>Computes the menu geometry for a window size.</summary>
    public static MenuLayout Compute(
        int windowWidth,
        int windowHeight,
        int maxPageWidth,
        bool twoColumn,
        float leftRatio = 0.4f
    )
    {
        int pageWidth = Math.Max(MIN_PAGE_WIDTH, Math.Min(windowWidth - 120, maxPageWidth));
        int pageHeight = Math.Max(MIN_PAGE_HEIGHT, Math.Min(windowHeight - 240, MAX_PAGE_HEIGHT));
        bool useTabs = twoColumn && pageWidth < NARROW_BREAKPOINT;

        var title = new IntRect(new IntVector2(0, 0), new IntVector2(pageWidth, PAGE_TITLE_HEIGHT));
        var footer = new IntRect(
            new IntVector2(0, pageHeight - FOOTER_HEIGHT),
            new IntVector2(pageWidth, FOOTER_HEIGHT)
        );

        int panelY = PAGE_TITLE_HEIGHT + PAGE_GAP;
        int panelBottom = footer.Top - PAGE_GAP;
        int panelHeight = Math.Max(0, panelBottom - panelY);

        IntRect left;
        IntRect right;
        IntRect tabs;

        if (useTabs)
        {
            left = default;
            tabs = new IntRect(new IntVector2(0, panelY), new IntVector2(pageWidth, TABS_HEIGHT));
            int paneY = tabs.Bottom;
            right = new IntRect(
                new IntVector2(0, paneY),
                new IntVector2(pageWidth, Math.Max(0, panelBottom - paneY))
            );
        }
        else if (twoColumn)
        {
            tabs = default;
            int leftWidth = Math.Clamp((int)(pageWidth * leftRatio), MIN_LEFT_WIDTH, pageWidth - MIN_RIGHT_WIDTH);
            left = new IntRect(new IntVector2(0, panelY), new IntVector2(leftWidth, panelHeight));
            right = new IntRect(
                new IntVector2(leftWidth + PANEL_GAP, panelY),
                new IntVector2(pageWidth - leftWidth - PANEL_GAP, panelHeight)
            );
        }
        else
        {
            tabs = default;
            left = default;
            right = new IntRect(new IntVector2(0, panelY), new IntVector2(pageWidth, panelHeight));
        }

        var returnButton = new IntRect(
            new IntVector2(0, footer.Top + 6),
            new IntVector2(RETURN_WIDTH, FOOTER_HEIGHT - 12)
        );
        var primaryButton = new IntRect(
            new IntVector2(pageWidth - PRIMARY_WIDTH, footer.Top + 6),
            new IntVector2(PRIMARY_WIDTH, FOOTER_HEIGHT - 12)
        );

        return new MenuLayout(
            pageWidth,
            pageHeight,
            useTabs,
            title,
            left,
            right,
            tabs,
            footer,
            returnButton,
            primaryButton
        );
    }
}
