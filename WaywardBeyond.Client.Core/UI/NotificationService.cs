using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Swordfish.Graphics;
using WaywardBeyond.Client.Core.Extensions;

namespace WaywardBeyond.Client.Core.UI;

internal class NotificationService : IDisposable
{
    private readonly ILogger<NotificationService> _logger;
    private readonly IWindowContext _windowContext;

    private readonly ConcurrentQueue<NotificationState> _pushedStates = [];
    private readonly List<NotificationState> _activeStates = [];

    public NotificationService(in ILogger<NotificationService> logger, in IWindowContext windowContext) {
        _logger = logger;
        _windowContext = windowContext;

        windowContext.Update += OnWindowUpdate;
    }

    public void Dispose()
    {
        _windowContext.Update -= OnWindowUpdate;
    }

    public void Push(Notification notification)
    {
        var state = new NotificationState(notification, DateTime.Now);
        _pushedStates.Enqueue(state);

        if (notification.Type != NotificationType.Action)
        {
            _logger.LogInformation("[Notification] {type}: {text}", notification.Type, notification.Text);
        }
    }

    public IEnumerable<NotificationState> GetActiveNotifications(NotificationType type)
    {
        return _activeStates.Where(state => state.Notification.Type == type);
    }
    
    private void OnWindowUpdate(double delta)
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
            if (elapsed.TotalMilliseconds < state.GetLifetime())
            {
                break;
            }
            
            _activeStates.RemoveAt(lastIndex);
        }
    }
}