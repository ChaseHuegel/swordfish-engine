using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Swordfish.Library.Types;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Voxels.Models;

namespace WaywardBeyond.Client.Core.Systems;

internal class InteractionState(in PlayerControllerSystem playerControllerSystem)
{
    public readonly DataBinding<BrickShape> SelectedShape = new(BrickShape.Block);
    public readonly DataBinding<Orientation> SelectedOrientation = new();
    public readonly DataBinding<bool> SnapPlacement = new();
    
    private readonly PlayerControllerSystem _playerControllerSystem = playerControllerSystem;
    
    private readonly HashSet<InteractionBlocker> _interactionBlockers = [];
    private InteractionBlocker? _inputBlocker;
    
    public InteractionBlocker BlockInteraction()
    {
        return new InteractionBlocker(this, _playerControllerSystem);
    }

    public bool TryBlockInteractionExclusive([NotNullWhen(true)] out InteractionBlocker? interactionBlocker)
    {
        lock (_interactionBlockers)
        {
            if (_interactionBlockers.Count != 0)
            {
                interactionBlocker = null;
                return false;
            }

            interactionBlocker = BlockInteraction();
            return true;
        }
    }
    
    public bool IsInteractionBlocked()
    {
        lock (_interactionBlockers)
        {
            return _interactionBlockers.Count != 0;
        }
    }
    
    internal void SetInteractionEnabled(bool enabled) 
    {
        lock (_interactionBlockers)
        {
            InteractionBlocker? blocker = _inputBlocker;
            blocker?.Dispose();
            _inputBlocker = enabled ? null : BlockInteraction();
        }
    }
    
    public sealed class InteractionBlocker : IDisposable
    {
        private readonly InteractionState _interactionState;
        private readonly PlayerControllerSystem _playerControllerSystem;

        internal InteractionBlocker(in InteractionState interactionState, in PlayerControllerSystem playerControllerSystem)
        {
            _interactionState = interactionState;
            _playerControllerSystem = playerControllerSystem;
            lock (interactionState._interactionBlockers)
            {
                interactionState._interactionBlockers.Add(this);
                playerControllerSystem.SetInputEnabled(false);
            }
        }

        public void Dispose()
        {
            lock (_interactionState._interactionBlockers)
            {
                _interactionState._interactionBlockers.Remove(this);
                _playerControllerSystem.SetInputEnabled(!_interactionState.IsInteractionBlocked());
            }
        }
    }
}