using System.Collections.Generic;
using Swordfish.Library.Events;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Events;
using WaywardBeyond.Client.Core.UI;

namespace WaywardBeyond.Client.Core.Skills.Listeners;

internal class PlaceEventXPListener(in SkillDatabase skillDatabase, in NotificationService notificationService) : IEventProcessor<PlaceEvent>
{
    private readonly SkillDatabase _skillDatabase = skillDatabase;
    private readonly NotificationService _notificationService = notificationService;
    private float xp = 0;

    public Result<EventBehavior> ProcessEvent(object sender, PlaceEvent e)
    {
        xp += 1;
        
        Result<Skill[]> skills = _skillDatabase.Get(XPSource.Place);
        if (!skills.Success)
        {
            return Result<EventBehavior>.FromSuccess(EventBehavior.Continue);
        }

        for (var i = 0; i < skills.Value.Length; i++)
        {
            Skill skill = skills.Value[i];
            Dictionary<string, int> sources = skill.Sources[XPSource.Place];
            if (!sources.TryGetValue(e.BrickInfo.ID, out int value))
            {
                return Result<EventBehavior>.FromSuccess(EventBehavior.Continue);
            }

            var notification = new Notification("Building", xp / 100f);
            _notificationService.Push(notification);
        }
        
        return Result<EventBehavior>.FromSuccess(EventBehavior.Continue);
    }
}