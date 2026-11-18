using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Util;
using WaywardBeyond.Data.Networking;

namespace WaywardBeyond.Client.UI.Layers;

internal class InteractionNotificationLayer(in NotificationService notificationService)
    : NotificationLayer(notificationService)
{
    public override NotificationType Type => NotificationType.Interaction;
    
    protected override bool HasBackground => true;
    protected override bool OnlyRenderLatest => true;

    public override bool IsVisible()
    {
        return true;
    }
    
    public override Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        IntVector2 cursorPosition = ui.GetCursorPosition();
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Local | Anchors.Bottom | Anchors.Center,
                X = new Fixed(cursorPosition.X),
                Y = new Fixed(cursorPosition.Y),
            };

            base.RenderUI(delta, ui);
        }
        
        return Result.FromSuccess();
    }
}
