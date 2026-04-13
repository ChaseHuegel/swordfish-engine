using Swordfish.Library.Events;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Events;
using WaywardBeyond.Client.Core.Globalization;
using WaywardBeyond.Client.Core.UI;

namespace WaywardBeyond.Client.Core.Skills.Listeners;

internal class LevelUpEventListener(in NotificationService notificationService, in LocalizedFormatter localizedFormatter)
    : IEventProcessor<LevelUpEvent>
{
    private readonly NotificationService _notificationService = notificationService;
    private readonly LocalizedFormatter _localizedFormatter = localizedFormatter;

    public Result<EventBehavior> ProcessEvent(object sender, LevelUpEvent e)
    {
        string message = _localizedFormatter.GetString("notification.skill.levelUp", e);
        var notification = new Notification(message);
        _notificationService.Push(notification);
        
        return Result<EventBehavior>.FromSuccess(EventBehavior.Continue);
    }
}