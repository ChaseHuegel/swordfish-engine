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

    public Result<EventBehavior> ProcessEvent(object sender, PlaceEvent e)
    {
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
            
            _notificationService.Push($"+{value} {skill.Name} XP");
        }
        
        return Result<EventBehavior>.FromSuccess(EventBehavior.Continue);
    }
}