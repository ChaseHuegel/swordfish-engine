using System;
using Swordfish.Library.Events;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Events;
using WaywardBeyond.Client.Core.Numerics;
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
        
        Int2 placed = character.AddStatistic("bricks.placed", 1);
        Int2 placedBrick = character.AddStatistic($"bricks.placed:{e.BrickInfo.ID}", 1);
        
        Console.WriteLine("Placed: " + placed.Current);
        Console.WriteLine($"Placed {e.BrickInfo.ID}: " + placedBrick.Current);
        
        _characterSaveManager.ActiveSave = new CharacterSave(save.Path, character);
        
        return Result<EventBehavior>.FromSuccess(EventBehavior.Continue);
    }
}