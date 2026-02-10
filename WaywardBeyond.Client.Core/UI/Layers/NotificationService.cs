using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Core.UI.Layers;

using ToastNotification = (Notification Notification, DateTime CreatedAt);

internal class NotificationService : IUILayer
{
    private const int MAX_LIFETIME_MS = 3_000;
    
    private readonly ConcurrentQueue<ToastNotification> _pushedNotifications = [];
    private readonly List<ToastNotification> _notifications = [];
    
    public void Push(Notification notification)
    {
        var toastNotification = new ToastNotification(notification, DateTime.Now);
        _pushedNotifications.Enqueue(toastNotification);
    }
    
    public bool IsVisible()
    {
        return true;
    }
    
    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        DateTime now = DateTime.Now;
        
        //  Collect notifications that have been pushed
        while (_pushedNotifications.TryDequeue(out ToastNotification pushedNotification))
        {
            _notifications.Insert(0, pushedNotification);
        }
        
        //  Remove expired notifications
        while (_notifications.Count > 0)
        {
            int lastIndex = _notifications.Count - 1;
            ToastNotification toastNotification = _notifications[lastIndex];
            
            TimeSpan elapsed = now - toastNotification.CreatedAt;
            if (elapsed.TotalMilliseconds < MAX_LIFETIME_MS)
            {
                break;
            }
            
            _notifications.RemoveAt(lastIndex);
        }
        
        //  Render toast notifications
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.Vertical;
            ui.Constraints = new Constraints
            {
                X = new Relative(0.01f),
                Y = new Relative(0.03f),
            };
            
            for (var i = 0; i < _notifications.Count; i++)
            {
                ToastNotification toastNotification = _notifications[i];
                if (toastNotification.Notification.Type != NotificationType.Toast)
                {
                    continue;
                }
                
                RenderNotification(ui, toastNotification, now);
            }
        }
        
        if (!WaywardBeyond.IsPlaying())
        {
            return Result.FromSuccess();
        }
        
        //  Render action notifications
        using (ui.Element())
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center | Anchors.Bottom,
                Y = new Fixed(-196),
            };
            
            for (var i = 0; i < _notifications.Count; i++)
            {
                ToastNotification toastNotification = _notifications[i];
                if (toastNotification.Notification.Type != NotificationType.Action)
                {
                    continue;
                }
                
                RenderNotification(ui, toastNotification, now);
                break;  //  Only render the most recent notification
            }
        }
        
        return Result.FromSuccess();
    }

    private static void RenderNotification(UIBuilder<Material> ui, ToastNotification toastNotification, DateTime now)
    {
        using (ui.Text(toastNotification.Notification.Text))
        {
            TimeSpan elapsed = now - toastNotification.CreatedAt;
                    
            float alpha = MathS.RangeToRange((float)elapsed.TotalMilliseconds, 0, MAX_LIFETIME_MS, 0f, 1f);
            //  Falloff near the end of the notification's lifetime
            //      Graph: https://www.desmos.com/calculator/udfsvtcbgn
            alpha = 1f - (float)Math.Pow(alpha, 9f);
                    
            ui.Color = new Vector4(1f, 1f, 1f, alpha);
        }
    }
}