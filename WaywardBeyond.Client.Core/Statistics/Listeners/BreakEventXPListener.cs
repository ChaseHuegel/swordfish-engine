using System;
using Swordfish.Library.Events;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Events;
using WaywardBeyond.Client.Core.Numerics;
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
        character.Statistics ??= [];
        
        Int2 broken = character.Statistics.Add("bricks.broken", 1);
        Int2 brokenBrick = character.Statistics.Add($"bricks.broken:{e.BrickInfo.ID}", 1);
        
        Console.WriteLine("Broken: " + broken.Current);
        Console.WriteLine($"Broken {e.BrickInfo.ID}: " + brokenBrick.Current);
        
        _characterSaveManager.ActiveSave = new CharacterSave(save.Path, character);
        
        return Result<EventBehavior>.FromSuccess(EventBehavior.Continue);
    }
}