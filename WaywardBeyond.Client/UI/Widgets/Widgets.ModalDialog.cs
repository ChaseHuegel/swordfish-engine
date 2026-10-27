using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    /// <summary>Lays out the scrim, center container, and panel of one modal dialog, and closes them in reverse order.</summary>
    public ref struct ModalScope(
        UIBuilder<Material>.Scope scrim,
        UIBuilder<Material>.Scope center,
        MenuPanelScope panel
    ) {
        private UIBuilder<Material>.Scope _scrim = scrim;
        private UIBuilder<Material>.Scope _center = center;
        private MenuPanelScope _panel = panel;

        public void Dispose()
        {
            _panel.Dispose();
            _center.Dispose();
            _scrim.Dispose();
        }
    }

    /// <summary>Creates a centered modal dialog with a scrim, a center container, and a borderless panel.</summary>
    public static ModalScope ModalDialog(
        this UIBuilder<Material> ui,
        int width,
        int height,
        Vector4? scrimColor = null
    ) {
        UIBuilder<Material>.Scope scrim = ui.Element();
        ui.LayoutDirection = LayoutDirection.None;
        ui.Color = scrimColor ?? UiTheme.Scrim;
        ui.Constraints = new Constraints
        {
            Width = new Fill(),
            Height = new Fill(),
        };

        UIBuilder<Material>.Scope center = ui.Element();
        ui.LayoutDirection = LayoutDirection.None;
        ui.Constraints = new Constraints
        {
            Anchors = Anchors.Center,
            Width = new Fixed(width),
            Height = new Fixed(height),
        };
        ui.ClipConstraints = ClipToBounds();

        MenuPanelScope panel = ui.ModalPanel(
            new Constraints
            {
                Width = new Fill(),
                Height = new Fill(),
            }
        );

        return new ModalScope(scrim, center, panel);
    }
}
