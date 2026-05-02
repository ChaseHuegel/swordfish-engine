using System.Collections.Generic;
using Swordfish.Library.Events;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Events;
using WaywardBeyond.Client.Core.Numerics;
using WaywardBeyond.Client.Core.Saves;

namespace WaywardBeyond.Client.Core.Skills.Listeners;

internal class BreakEventXPListener(
    in SkillDatabase skillDatabase,
    in CharacterSaveManager characterSaveManager,
    in EventInvoker<XPEvent> xpEvent,
    in EventInvoker<LevelUpEvent> levelUpEvent
) : IEventProcessor<BreakEvent>
{
    private readonly SkillDatabase _skillDatabase = skillDatabase;
    private readonly CharacterSaveManager _characterSaveManager = characterSaveManager;
    private readonly EventInvoker<XPEvent> _xpEvent = xpEvent;
    private readonly EventInvoker<LevelUpEvent> _levelUpEvent = levelUpEvent;

    public Result<EventBehavior> ProcessEvent(object sender, BreakEvent e)
    {
        Result<Skill[]> skills = _skillDatabase.Get(XPSource.Break);
        if (!skills.Success)
        {
            return Result<EventBehavior>.FromSuccess(EventBehavior.Continue);
        }

        for (var i = 0; i < skills.Value.Length; i++)
        {
            Skill skill = skills.Value[i];
            Dictionary<string, int> sources = skill.Sources[XPSource.Break];
            if (!sources.TryGetValue(e.BrickInfo.ID, out int sourceXP))
            {
                continue;
            }

            CharacterSave? activeSave = _characterSaveManager.ActiveSave;
            if (activeSave == null)
            {
                return Result<EventBehavior>.FromSuccess(EventBehavior.Continue);
            }

            CharacterSave save = activeSave.Value;
            Character character = save.Character;
            character.Statistics ??= [];

            Int2 change = character.Statistics.Add(skill.ID, sourceXP);
            _characterSaveManager.ActiveSave = new CharacterSave(save.Path, character);

            LevelInfo prevLvl = CalculateCurrentLevel(skill, change.Previous);
            LevelInfo currLvl = CalculateCurrentLevel(skill, change.Current);

            var xpEvent = new XPEvent(skill, currLvl.Level, currLvl.XP, sourceXP);
            _xpEvent.Invoke(xpEvent);

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
        var currentLevel = 0;
        var totalXP = 0;
        foreach (KeyValuePair<int, int> level in skill.Levels)
        {
            if (totalXP + level.Value < currentXP)
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