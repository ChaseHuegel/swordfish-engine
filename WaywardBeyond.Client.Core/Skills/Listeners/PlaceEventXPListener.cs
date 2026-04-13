using System.Collections.Generic;
using System.Linq;
using Swordfish.Library.Events;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Events;
using WaywardBeyond.Client.Core.Saves;
using WaywardBeyond.Client.Core.UI;

namespace WaywardBeyond.Client.Core.Skills.Listeners;

internal class PlaceEventXPListener(
    in SkillDatabase skillDatabase,
    in NotificationService notificationService,
    in CharacterSaveManager characterSaveManager,
    in EventInvoker<LevelUpEvent> levelUpEvent
) : IEventProcessor<PlaceEvent>
{
    private readonly SkillDatabase _skillDatabase = skillDatabase;
    private readonly NotificationService _notificationService = notificationService;
    private readonly CharacterSaveManager _characterSaveManager = characterSaveManager;
    private readonly EventInvoker<LevelUpEvent> _levelUpEvent = levelUpEvent;

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
            if (!sources.TryGetValue(e.BrickInfo.ID, out int sourceXP))
            {
                return Result<EventBehavior>.FromSuccess(EventBehavior.Continue);
            }

            CharacterSave? activeSave = _characterSaveManager.ActiveSave;
            if (activeSave == null)
            {
                return Result<EventBehavior>.FromSuccess(EventBehavior.Continue);
            }

            CharacterSave save = activeSave.Value;
            Character character = save.Character;
            character.Statistics ??= [];

            int skillStatisticIndex = -1;
            Statistic skillStatistic = default;
            for (var n = 0; n < character.Statistics.Length; n++)
            {
                Statistic statistic = character.Statistics[n];
                if (statistic.ID != skill.ID)
                {
                    continue;
                }
                
                skillStatistic = statistic;
                skillStatisticIndex = n;
                break;
            }

            if (skillStatistic.ID == null)
            {
                skillStatistic = new Statistic(skill.ID, 0);
            }

            if (skillStatisticIndex == -1)
            {
                skillStatisticIndex = character.Statistics.Length;
                
                Statistic[] oldArr = character.Statistics;
                character.Statistics = new Statistic[oldArr.Length + 1];
                oldArr.CopyTo(character.Statistics, 0);
            }
            
            LevelInfo prevLvl = CalculateCurrentLevel(skill, skillStatistic.Value);
            
            skillStatistic.Value += sourceXP;
            
            character.Statistics[skillStatisticIndex] = skillStatistic;

            _characterSaveManager.ActiveSave = new CharacterSave(save.Path, character);

            LevelInfo currLvl = CalculateCurrentLevel(skill, skillStatistic.Value);

            if (!skill.Levels.TryGetValue(currLvl.Level + 1, out int nextLevelXP))
            {
                nextLevelXP = 1;
            }
            
            var notification = new Notification(skill.Name, (float)currLvl.XP / nextLevelXP);
            _notificationService.Push(notification);

            if (prevLvl.Level != currLvl.Level)
            {
                var levelUpEvent = new LevelUpEvent(skill, prevLvl.Level, currLvl.Level);
                _levelUpEvent.Invoke(levelUpEvent);
            }
        }
        
        return Result<EventBehavior>.FromSuccess(EventBehavior.Continue);
    }
    
    private LevelInfo CalculateCurrentLevel(Skill skill, int currentXP)
    {
        IOrderedEnumerable<KeyValuePair<int, int>> levels = skill.Levels.OrderBy(kvp => kvp.Key);

        int currentLevel = 0;
        int totalXP = 0;
        foreach (KeyValuePair<int, int> level in levels)
        {
            if (totalXP < currentXP)
            {
                totalXP += level.Value;
                currentLevel = level.Key;
            }
            else
            {
                break;
            }
        }
        
        return new LevelInfo(currentLevel, currentXP - totalXP);
    }

    private record struct LevelInfo(int Level, int XP);
}