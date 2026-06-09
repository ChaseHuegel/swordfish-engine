using Swordfish.Library.Events;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Events;
using WaywardBeyond.Client.Core.Saves;
using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Client.Core.Statistics.Listeners;

internal class PlaceEventStatisticListener(in CharacterSaveManager characterSaveManager )
    : IEventProcessor<PlaceEvent>
{
    private readonly CharacterSaveManager _characterSaveManager = characterSaveManager;

    public Result<EventBehavior> ProcessEvent(object sender, PlaceEvent e)
    {
        Character? activeSave = _characterSaveManager.ActiveSave;
        if (activeSave == null)
        {
            return Result<EventBehavior>.FromSuccess(EventBehavior.Continue);
        }

        Character character = activeSave.Value;
        
        character.AddStatistic("bricks.placed", 1);
        character.AddStatistic($"bricks.placed:{e.BrickInfo.ID}", 1);
        
        _characterSaveManager.ActiveSave = character;
        
        return Result<EventBehavior>.FromSuccess(EventBehavior.Continue);
    }
}