using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Core.UI.Layers;

internal class ToastNotificationLayer(in NotificationService notificationService)
    : NotificationLayer(notificationService)
{
    public override NotificationType Type => NotificationType.Toast;
    
    protected override bool HasBackground => false;
    protected override bool OnlyRenderLatest => false;

    public override bool IsVisible()
    {
        return true;
    }
    
    public override Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        using (ui.Element())
        {
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