using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Core.UI.Layers;

internal class InteractionNotificationLayer(in IInputService inputService, in NotificationService notificationService)
    : NotificationLayer(notificationService)
{
    private readonly IInputService _inputService = inputService;
    
    public override NotificationType Type => NotificationType.Interaction;
    
    protected override bool HasBackground => true;
    protected override bool OnlyRenderLatest => true;

    public override bool IsVisible()
    {
        return true;
    }
    
    public override Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        Vector2 cursorPosition = _inputService.CursorPosition;
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Local | Anchors.Bottom | Anchors.Center,
                X = new Fixed((int)cursorPosition.X),
                Y = new Fixed((int)cursorPosition.Y),
            };

            base.RenderUI(delta, ui);
        }
        
        return Result.FromSuccess();
    }
}