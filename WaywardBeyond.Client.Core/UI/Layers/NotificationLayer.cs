using System;
using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Extensions;

namespace WaywardBeyond.Client.Core.UI.Layers;

internal abstract class NotificationLayer(in NotificationService notificationService) : IUILayer
{
    private readonly NotificationService _notificationService = notificationService;
    
    public abstract NotificationType Type { get; }

    protected abstract bool HasBackground { get; }
    protected abstract bool OnlyRenderLatest { get; }
    
    public abstract bool IsVisible();
    
    public virtual Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        DateTime now = DateTime.Now;
        
        foreach (NotificationState state in _notificationService.GetActiveNotifications(Type))
        {
            state.Render(ui, now, HasBackground);

            if (OnlyRenderLatest)
            {
                break;
            }
        }
        
        return Result.FromSuccess();
    }
}