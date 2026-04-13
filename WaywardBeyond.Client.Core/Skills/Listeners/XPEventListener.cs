using Swordfish.Library.Events;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Events;
using WaywardBeyond.Client.Core.Globalization;
using WaywardBeyond.Client.Core.UI;

namespace WaywardBeyond.Client.Core.Skills.Listeners;

internal class XPEventListener(in NotificationService notificationService, in LocalizedFormatter localizedFormatter)
    : IEventProcessor<XPEvent>
{
    private readonly NotificationService _notificationService = notificationService;
    private readonly LocalizedFormatter _localizedFormatter = localizedFormatter;

    public Result<EventBehavior> ProcessEvent(object sender, XPEvent e)
    {
        if (!e.Skill.Levels.TryGetValue(e.Level + 1, out int nextLevelXP))
        {
            nextLevelXP = 1;
        }
        
        string title = _localizedFormatter.GetString("notification.skill.bar", e);
        var notification = new Notification(e.Skill.ID, title, (float)e.XP / nextLevelXP);
        _notificationService.Push(notification);
        
        return Result<EventBehavior>.FromSuccess(EventBehavior.Continue);
    }
}