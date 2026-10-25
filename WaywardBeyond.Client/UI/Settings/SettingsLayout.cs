using System;
using Reef;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>Fixed-metric geometry for the settings page at one window size.</summary>
internal readonly record struct SettingsLayout(
    int PageWidth,
    int PageHeight,
    bool UseTabs,
    IntRect Tray,
    IntRect Pane,
    IntRect Tabs,
    IntRect Footer,
    IntRect List,
    IntRect Scrollbar,
    int LabelWidth,
    int ControlWidth
)
{
    private const int MAX_PAGE_WIDTH = 1280;
    private const int MAX_PAGE_HEIGHT = 840;
    private const int PAGE_GAP = 12;
    private const int PANEL_GAP = 16;
    private const int FOOTER_HEIGHT = 48;
    private const int PANE_PADDING = 24;
    private const int PANE_HEADER_HEIGHT = 64;
    private const int SCROLLBAR_WIDTH = 6;
    private const int SCROLLBAR_GAP = 8;
    private const int TABS_HEIGHT = 40;
    private const int ROW_GAP = 16;
    private const int MIN_CONTROL_WIDTH = 220;
    private const int MAX_CONTROL_WIDTH = 340;
    private const int MIN_LABEL_WIDTH = 120;

    /// <summary>Computes the settings page geometry for a window size.</summary>
    public static SettingsLayout Compute(int windowWidth, int windowHeight)
    {
        int pageWidth = Math.Max(320, Math.Min(windowWidth - 80, MAX_PAGE_WIDTH));
        int pageHeight = Math.Max(280, Math.Min(windowHeight - 240, MAX_PAGE_HEIGHT));
        bool useTabs = windowWidth < 900;

        var footer = new IntRect(
            new IntVector2(0, pageHeight - FOOTER_HEIGHT),
            new IntVector2(pageWidth, FOOTER_HEIGHT)
        );
        int panelY = PAGE_GAP;
        int panelBottom = footer.Top - PAGE_GAP;
        int panelHeight = Math.Max(0, panelBottom - panelY);

        IntRect tray;
        IntRect pane;
        IntRect tabs;

        if (useTabs)
        {
            tray = default;
            tabs = new IntRect(new IntVector2(0, panelY), new IntVector2(pageWidth, TABS_HEIGHT));
            int paneY = tabs.Bottom + PAGE_GAP;
            pane = new IntRect(
                new IntVector2(0, paneY),
                new IntVector2(pageWidth, Math.Max(0, panelBottom - paneY))
            );
        }
        else
        {
            tabs = default;
            int trayWidth = Math.Clamp((int)(pageWidth * 0.24f), 220, 300);
            tray = new IntRect(new IntVector2(0, panelY), new IntVector2(trayWidth, panelHeight));
            pane = new IntRect(
                new IntVector2(trayWidth + PANEL_GAP, panelY),
                new IntVector2(pageWidth - trayWidth - PANEL_GAP, panelHeight)
            );
        }

        int listWidth = Math.Max(1, pane.Size.X - PANE_PADDING * 2 - SCROLLBAR_WIDTH - SCROLLBAR_GAP);
        int listHeight = Math.Max(1, pane.Size.Y - PANE_HEADER_HEIGHT - PANE_PADDING);
        var list = new IntRect(
            new IntVector2(pane.Left + PANE_PADDING, pane.Top + PANE_HEADER_HEIGHT),
            new IntVector2(listWidth, listHeight)
        );
        var scrollbar = new IntRect(
            new IntVector2(pane.Right - PANE_PADDING - SCROLLBAR_WIDTH, list.Top),
            new IntVector2(SCROLLBAR_WIDTH, listHeight)
        );

        int controlWidth = Math.Clamp(listWidth / 3, MIN_CONTROL_WIDTH, MAX_CONTROL_WIDTH);
        controlWidth = Math.Min(controlWidth, Math.Max(MIN_LABEL_WIDTH, listWidth - MIN_LABEL_WIDTH - ROW_GAP));
        int labelWidth = Math.Max(0, listWidth - controlWidth - ROW_GAP);

        return new SettingsLayout(
            pageWidth,
            pageHeight,
            useTabs,
            tray,
            pane,
            tabs,
            footer,
            list,
            scrollbar,
            labelWidth,
            controlWidth
        );
    }
}
