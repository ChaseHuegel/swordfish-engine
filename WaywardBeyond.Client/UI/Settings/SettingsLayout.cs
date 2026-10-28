using System;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>Responsive geometry for the settings page at one window size.</summary>
internal readonly record struct SettingsLayout(
    int PageWidth,
    int PageHeight,
    bool UseTabs,
    int TrayWidth,
    int LabelWidth,
    int ControlWidth
)
{
    public const int MAX_PAGE_WIDTH = 1280;
    public const int MAX_PAGE_HEIGHT = 840;
    public const int PAGE_GAP = 12;
    public const int PANEL_GAP = 16;
    public const int FOOTER_HEIGHT = 48;
    public const int PANE_PADDING = 24;
    public const int SCROLLBAR_WIDTH = 6;
    public const int SCROLLBAR_GAP = 8;
    public const int TABS_HEIGHT = 40;
    public const int ROW_GAP = 16;
    public const int MIN_CONTROL_WIDTH = 220;
    public const int MAX_CONTROL_WIDTH = 340;
    public const int MIN_LABEL_WIDTH = 120;

    /// <summary>Computes the settings page geometry for a window size.</summary>
    public static SettingsLayout Compute(int windowWidth, int windowHeight)
    {
        int pageWidth = Math.Max(320, Math.Min(windowWidth - 80, MAX_PAGE_WIDTH));
        int pageHeight = Math.Max(280, Math.Min(windowHeight - 240, MAX_PAGE_HEIGHT));
        bool useTabs = windowWidth < 900;

        int trayWidth = useTabs
            ? 0
            : Math.Clamp((int)(pageWidth * 0.24f), 220, 300);
        int paneWidth = useTabs
            ? pageWidth
            : pageWidth - trayWidth - PANEL_GAP;

        int listWidth = Math.Max(
            1,
            paneWidth - PANE_PADDING * 2 - SCROLLBAR_WIDTH - SCROLLBAR_GAP
        );
        int controlWidth = Math.Clamp(listWidth / 3, MIN_CONTROL_WIDTH, MAX_CONTROL_WIDTH);
        controlWidth = Math.Min(
            controlWidth,
            Math.Max(MIN_LABEL_WIDTH, listWidth - MIN_LABEL_WIDTH - ROW_GAP)
        );
        int labelWidth = Math.Max(0, listWidth - controlWidth - ROW_GAP);

        return new SettingsLayout(
            pageWidth,
            pageHeight,
            useTabs,
            trayWidth,
            labelWidth,
            controlWidth
        );
    }
}
