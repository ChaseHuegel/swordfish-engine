using System;
using System.Numerics;
using Swordfish.Library.Events;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Events;
using WaywardBeyond.Client.Core.Saves;
using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Client.Core.Statistics.Listeners;

internal class PlayerMovedStatisticListener(in ActiveCharacterSave activeCharacterSave)
    : IEventProcessor<PlayerMovedEvent>
{
    private readonly ActiveCharacterSave _activeCharacterSave = activeCharacterSave;

    private Vector3? _previousPosition;
    private float _accumulatedDistance;

    public Result<EventBehavior> ProcessEvent(object sender, PlayerMovedEvent e)
    {
        if (_previousPosition == null)
        {
            _previousPosition = e.Position;
            return Result<EventBehavior>.FromSuccess(EventBehavior.Continue);
        }
        
        Character? activeSave = _activeCharacterSave.ActiveSave;
        if (activeSave == null)
        {
            return Result<EventBehavior>.FromSuccess(EventBehavior.Continue);
        }

        Character character = activeSave.Value;

        Vector3 positionDelta = e.Position - _previousPosition.Value;
        float distance = Math.Abs(positionDelta.Length());
        _previousPosition = e.Position;
        
        //  Large distance changes are likely teleports and should not count toward travel 
        if (distance > 5f)
        {
            return Result<EventBehavior>.FromSuccess(EventBehavior.Continue);
        }
        
        _accumulatedDistance += distance;
        if (_accumulatedDistance >= 1f)
        {
            float remainder = _accumulatedDistance % 1;
            var meters = (int)(_accumulatedDistance - remainder);
            _accumulatedDistance = remainder;
            
            character.AddStatistic("distance.meters.traveled", meters);
            character.AddStatistic("distance.meters.traveled.eva", meters);
        }
        
        _activeCharacterSave.ActiveSave = character;

        return Result<EventBehavior>.FromSuccess(EventBehavior.Continue);
    }
}