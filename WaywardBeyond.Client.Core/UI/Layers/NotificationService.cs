using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Core.UI.Layers;

using NotificationState = (Notification Notification, DateTime CreatedAt);

internal class NotificationService(in IInputService inputService) : IUILayer
{
    private readonly IInputService _inputService = inputService;
    
    private readonly ConcurrentQueue<NotificationState> _pushedStates = [];
    private readonly List<NotificationState> _activeStates = [];
    
    public void Push(Notification notification)
    {
        var state = new NotificationState(notification, DateTime.Now);
        _pushedStates.Enqueue(state);
    }
    
    public bool IsVisible()
    {
        return true;
    }
    
    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        DateTime now = DateTime.Now;
        
        //  Collect notifications that have been pushed
        while (_pushedStates.TryDequeue(out NotificationState pushedNotification))
        {
            _activeStates.Insert(0, pushedNotification);
        }
        
        //  Remove expired notifications
        while (_activeStates.Count > 0)
        {
            int lastIndex = _activeStates.Count - 1;
            NotificationState state = _activeStates[lastIndex];
            
            TimeSpan elapsed = now - state.CreatedAt;
            if (elapsed.TotalMilliseconds < GetLifetime(state.Notification.Type))
            {
                break;
            }
            
            _activeStates.RemoveAt(lastIndex);
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
            
            for (var i = 0; i < _activeStates.Count; i++)
            {
                NotificationState state = _activeStates[i];
                if (state.Notification.Type != NotificationType.Toast)
                {
                    continue;
                }
                
                RenderNotification(ui, state, now);
            }
        }
        
        //  Render interaction notifications
        for (var i = 0; i < _activeStates.Count; i++)
        {
            NotificationState state = _activeStates[i];
            if (state.Notification.Type != NotificationType.Interaction)
            {
                continue;
            }
         
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
                
                RenderNotification(ui, state, now);
                break;  //  Only render the most recent notification
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
            
            for (var i = 0; i < _activeStates.Count; i++)
            {
                NotificationState state = _activeStates[i];
                if (state.Notification.Type != NotificationType.Action)
                {
                    continue;
                }
                
                RenderNotification(ui, state, now);
                break;  //  Only render the most recent notification
            }
        }
        
        return Result.FromSuccess();
    }

    private static void RenderNotification(UIBuilder<Material> ui, NotificationState state, DateTime now)
    {
        using (ui.Text(state.Notification.Text))
        {
            TimeSpan elapsed = now - state.CreatedAt;
                    
            float alpha = MathS.RangeToRange((float)elapsed.TotalMilliseconds, 0, GetLifetime(state.Notification.Type), 0f, 1f);
            //  Falloff near the end of the notification's lifetime
            //      Graph: https://www.desmos.com/calculator/udfsvtcbgn
            alpha = 1f - (float)Math.Pow(alpha, 9f);
            
            ui.Color = new Vector4(1f, 1f, 1f, alpha);
            ui.BackgroundColor = new Vector4(0f, 0f, 0f, 0.75f * alpha);
        }
    }

    private static int GetLifetime(NotificationType type)
    {
        return type switch
        {
            NotificationType.Interaction => 1500,
            _ => 3000,
        };
    }
}