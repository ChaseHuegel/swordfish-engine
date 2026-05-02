using System;
using System.Numerics;
using Swordfish.Library.Events;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Events;
using WaywardBeyond.Client.Core.Saves;

namespace WaywardBeyond.Client.Core.Statistics.Listeners;

internal class PlayerMovedStatisticListener(in CharacterSaveManager characterSaveManager)
    : IEventProcessor<PlayerMovedEvent>
{
    private readonly CharacterSaveManager _characterSaveManager = characterSaveManager;

    private Vector3? _previousPosition;
    private float _accumulatedDistance;

    public Result<EventBehavior> ProcessEvent(object sender, PlayerMovedEvent e)
    {
        if (_previousPosition == null)
        {
            _previousPosition = e.Position;
            return Result<EventBehavior>.FromSuccess(EventBehavior.Continue);
        }
        
        CharacterSave? activeSave = _characterSaveManager.ActiveSave;
        if (activeSave == null)
        {
            return Result<EventBehavior>.FromSuccess(EventBehavior.Continue);
        }

        CharacterSave save = activeSave.Value;
        Character character = save.Character;

        Vector3 positionDelta = e.Position - _previousPosition.Value;
        float distance = Math.Abs(positionDelta.Length());
        _accumulatedDistance += distance;

        if (_accumulatedDistance >= 1f)
        {
            float remainder = _accumulatedDistance % 1;
            var meters = (int)(_accumulatedDistance - remainder);
            _accumulatedDistance = remainder;
            
            character.AddStatistic("traveled.meters", meters);
        }
        
        _previousPosition = e.Position;
        _characterSaveManager.ActiveSave = new CharacterSave(save.Path, character);

        return Result<EventBehavior>.FromSuccess(EventBehavior.Continue);
    }
}