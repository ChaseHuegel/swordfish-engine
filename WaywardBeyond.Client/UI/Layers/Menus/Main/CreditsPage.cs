using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI.Layers.Menus.Main;

internal sealed class CreditsPage(
    in SoundEffectService soundEffectService,
    in ILocalization localization
) : IMenuPage<MenuPage>
{
    public MenuPage ID => MenuPage.Credits;

    private readonly ILocalization _localization = localization;
    private readonly Widgets.ActionButtonOptions _returnButton = Widgets.ActionButtonOptions.Menu(
        ButtonVariant.Secondary,
        soundEffectService
    );

    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<MenuPage> menu)
    {
        MenuLayout layout = MenuLayout.Compute(
            ui.Width,
            ui.Height,
            maxPageWidth: 820,
            twoColumn: false
        );

        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
                Width = new Fixed(layout.PageWidth),
                Height = new Fixed(layout.PageHeight),
            };
            ui.ClipConstraints = new Constraints
            {
                Width = new Fixed(layout.PageWidth),
                Height = new Fixed(layout.PageHeight),
            };

            ui.Label(
                _localization.GetString("ui.menu.credits")!,
                layout.Title,
                UiMetrics.MenuTitleFont,
                UiTheme.Text,
                Anchors.Center
            );

            IntRect panel = Widgets.Rect(
                0,
                layout.Right.Top,
                layout.PageWidth,
                layout.Right.Size.Y
            );

            using (ui.BracketPanel(panel, _localization.GetString("ui.credits.manifest")!))
            {
                int cx = layout.PageWidth / 2;

                DrawCentered(ui, _localization.GetString("ui.credits.producedBy")!, cx, 42, UiTheme.TextMuted, UiMetrics.MenuSmallFont);
                DrawCentered(ui, "BITFISH LLC", cx, 58, UiTheme.Primary, UiMetrics.MenuTitleFont);

                DrawCentered(ui, _localization.GetString("ui.credits.development")!, cx, 102, UiTheme.Accent, UiMetrics.MenuSmallFont);
                DrawCentered(ui, "CHASE M. HUEGEL", cx, 118, UiTheme.Text, UiMetrics.MenuBodyFont);

                DrawCentered(ui, _localization.GetString("ui.credits.engine")!, cx, 146, UiTheme.Accent, UiMetrics.MenuSmallFont);
                DrawCentered(ui, "SWORDFISH CUSTOM ENGINE", cx, 162, UiTheme.Text, UiMetrics.MenuBodyFont);

                DrawCentered(ui, _localization.GetString("ui.credits.contributions")!, cx, 190, UiTheme.Accent, UiMetrics.MenuSmallFont);
                DrawCentered(ui, _localization.GetString("ui.credits.audioVisual")!, cx, 206, UiTheme.Text, UiMetrics.MenuBodyFont);

                ui.Divider(Widgets.Rect(36, 230, layout.PageWidth - 72, 1), UiTheme.Border);

                DrawCentered(ui, _localization.GetString("ui.credits.thanks")!, cx, 240, UiTheme.TextMuted, UiMetrics.MenuSmallFont);
                DrawCentered(ui, _localization.GetString("ui.credits.community")!, cx, 256, UiTheme.TextMuted, UiMetrics.MenuSmallFont);
            }

            if (ui.ActionButton("Button_Back", _localization.GetString("ui.button.return")!, _returnButton with { Rect = layout.ReturnButton }))
            {
                menu.GoToPage(MenuPage.Home);
            }
        }

        return Result.FromSuccess();
    }

    private static void DrawCentered(
        UIBuilder<Material> ui,
        string text,
        int centerX,
        int y,
        System.Numerics.Vector4 color,
        int fontSize
    )
    {
        ui.Label(
            text,
            Widgets.Rect(0, y, centerX * 2, 18),
            fontSize,
            color,
            Anchors.Center
        );
    }
}
