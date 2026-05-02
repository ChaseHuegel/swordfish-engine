using Swordfish.Library.Events;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Events;
using WaywardBeyond.Client.Core.Saves;

namespace WaywardBeyond.Client.Core.Statistics.Listeners;

internal class PlaceEventStatisticListener(in CharacterSaveManager characterSaveManager )
    : IEventProcessor<PlaceEvent>
{
    private readonly CharacterSaveManager _characterSaveManager = characterSaveManager;

    public Result<EventBehavior> ProcessEvent(object sender, PlaceEvent e)
    {
        CharacterSave? activeSave = _characterSaveManager.ActiveSave;
        if (activeSave == null)
        {
            return Result<EventBehavior>.FromSuccess(EventBehavior.Continue);
        }

        CharacterSave save = activeSave.Value;
        Character character = save.Character;
        
        character.AddStatistic("bricks.placed", 1);
        character.AddStatistic($"bricks.placed:{e.BrickInfo.ID}", 1);
        
        _characterSaveManager.ActiveSave = new CharacterSave(save.Path, character);
        
        return Result<EventBehavior>.FromSuccess(EventBehavior.Continue);
    }
}