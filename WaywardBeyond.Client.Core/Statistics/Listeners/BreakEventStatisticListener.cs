using Swordfish.Library.Events;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Events;
using WaywardBeyond.Client.Core.Saves;

namespace WaywardBeyond.Client.Core.Statistics.Listeners;

internal class BreakEventStatisticListener(in CharacterSaveManager characterSaveManager)
    : IEventProcessor<BreakEvent>
{
    private readonly CharacterSaveManager _characterSaveManager = characterSaveManager;

    public Result<EventBehavior> ProcessEvent(object sender, BreakEvent e)
    {
        CharacterSave? activeSave = _characterSaveManager.ActiveSave;
        if (activeSave == null)
        {
            return Result<EventBehavior>.FromSuccess(EventBehavior.Continue);
        }

        CharacterSave save = activeSave.Value;
        Character character = save.Character;
        
        character.AddStatistic("bricks.broken", 1);
        character.AddStatistic($"bricks.broken:{e.BrickInfo.ID}", 1);
        
        _characterSaveManager.ActiveSave = new CharacterSave(save.Path, character);
        
        return Result<EventBehavior>.FromSuccess(EventBehavior.Continue);
    }
}