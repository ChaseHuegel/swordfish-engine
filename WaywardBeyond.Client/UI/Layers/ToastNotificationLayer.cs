using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Util;
using WaywardBeyond.Data.Networking;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>Renders stacked toast notifications in the top-left corner of the viewport.</summary>
internal sealed class ToastNotificationLayer(in NotificationService notificationService)
    : NotificationLayer(notificationService)
{
    /// <inheritdoc/>
    public override NotificationType Type => NotificationType.Toast;

    /// <inheritdoc/>
    protected override bool HasBackground => true;

    /// <inheritdoc/>
    protected override bool OnlyRenderLatest => false;

    /// <inheritdoc/>
    public override bool IsVisible() => true;

    /// <inheritdoc/>
    public override Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        using (ui.Element())
        {
            ui.Passthrough = true;
            ui.Spacing = 6;
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Constraints = new Constraints
            {
                X = new Relative(0.01f),
                Y = new Relative(0.03f),
            };

            base.RenderUI(delta, ui);
        }

        return Result.FromSuccess();
    }
}
