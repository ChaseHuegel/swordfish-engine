using Krill;
using Swordfish.ECS;

namespace Swordfish.Graphics;

/// <summary>Retires mesh renderers when their component is removed or replaced.</summary>
public sealed class MeshRendererComponentProcessor : ComponentResourceProcessor<MeshRendererComponent>
{
    private readonly Dictionary<Uuid, MeshRenderer> _live = [];
    private readonly List<PendingRetirement> _pending = [];
    private readonly List<Uuid> _vanished = [];
    private int _seenCount;

    /// <inheritdoc/>
    protected override void ProcessLive(DataStore store, float delta)
    {
        _seenCount = 0;
        var action = new TrackAction(this);
        store.Query<MeshRendererComponent, TrackAction>(delta, ref action);

        //  Every live renderer was seen, so no tracked entry can have vanished.
        if (_seenCount != _live.Count)
        {
            PruneVanished(store);
        }
    }

    /// <inheritdoc/>
    protected override void OnRemoved(in MeshRendererComponent component, int entity)
    {
        MeshRenderer? renderer = component.MeshRenderer;
        if (renderer == null)
        {
            return;
        }

        //  The same renderer may have moved to another live entity in this tick.
        if (IsLive(renderer))
        {
            return;
        }

        RemoveLive(renderer);
        Enqueue(renderer, replacement: null);
    }

    /// <inheritdoc/>
    protected override void ProcessRetirements(DataStore store, float delta)
    {
        for (var i = _pending.Count - 1; i >= 0; i--)
        {
            PendingRetirement pending = _pending[i];
            pending.Age++;

            if (pending.CanRetire())
            {
                pending.Retire();
                _pending.RemoveAt(i);
            }
            else
            {
                _pending[i] = pending;
            }
        }
    }

    private void Enqueue(MeshRenderer renderer, MeshRenderer? replacement)
    {
        for (var i = 0; i < _pending.Count; i++)
        {
            if (ReferenceEquals(_pending[i].Renderer, renderer))
            {
                return;
            }
        }

        _pending.Add(new PendingRetirement(renderer, replacement));
    }

    private void Cancel(MeshRenderer renderer)
    {
        for (var i = _pending.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(_pending[i].Renderer, renderer))
            {
                _pending.RemoveAt(i);
            }
        }
    }

    /// <summary>Retires tracked renderers whose component is no longer present.</summary>
    private void PruneVanished(DataStore store)
    {
        _vanished.Clear();
        foreach (KeyValuePair<Uuid, MeshRenderer> pair in _live)
        {
            if (!store.TryGet(pair.Key, out MeshRendererComponent _))
            {
                _vanished.Add(pair.Key);
            }
        }

        for (var i = 0; i < _vanished.Count; i++)
        {
            Uuid uuid = _vanished[i];
            if (_live.Remove(uuid, out MeshRenderer? renderer))
            {
                if (IsLive(renderer))
                {
                    //  The same renderer was re-added under another entity.
                    Cancel(renderer);
                }
                else
                {
                    Enqueue(renderer, replacement: null);
                }
            }
        }
    }

    private bool IsLive(MeshRenderer renderer)
    {
        foreach (KeyValuePair<Uuid, MeshRenderer> pair in _live)
        {
            if (ReferenceEquals(pair.Value, renderer))
            {
                return true;
            }
        }

        return false;
    }

    private void RemoveLive(MeshRenderer renderer)
    {
        Uuid key = Uuid.Null;
        foreach (KeyValuePair<Uuid, MeshRenderer> pair in _live)
        {
            if (ReferenceEquals(pair.Value, renderer))
            {
                key = pair.Key;
                break;
            }
        }

        if (key != Uuid.Null)
        {
            _live.Remove(key);
        }
    }

    private readonly struct TrackAction : IForEach<MeshRendererComponent>
    {
        private readonly MeshRendererComponentProcessor _owner;

        public TrackAction(MeshRendererComponentProcessor owner)
        {
            _owner = owner;
        }

        public void Execute(float delta, DataStore store, int entity, in MeshRendererComponent component)
        {
            Uuid uuid = store.GetUuid(entity);
            MeshRenderer? renderer = component.MeshRenderer;
            if (renderer == null)
            {
                if (_owner._live.Remove(uuid, out MeshRenderer? removed))
                {
                    if (_owner.IsLive(removed))
                    {
                        //  The same renderer is still live under another entity.
                        _owner.Cancel(removed);
                    }
                    else
                    {
                        _owner.Enqueue(removed, replacement: null);
                    }
                }

                return;
            }

            if (_owner._live.TryGetValue(uuid, out MeshRenderer? previous))
            {
                if (ReferenceEquals(previous, renderer))
                {
                    _owner._seenCount++;
                    return;
                }

                _owner._live[uuid] = renderer;
                if (!_owner.IsLive(previous))
                {
                    _owner.Enqueue(previous, replacement: renderer);
                }
            }
            else
            {
                _owner._live[uuid] = renderer;
            }

            if (_owner._pending.Count > 0)
            {
                //  A renderer that returns or moves onto another entity before retirement is live again.
                _owner.Cancel(renderer);
            }

            _owner._seenCount++;
        }
    }

    /// <summary>A renderer waits for its replacement to bind before it is released.</summary>
    private struct PendingRetirement(in MeshRenderer renderer, in MeshRenderer? replacement)
    {
        private const int REPLACEMENT_MAX_AGE = 5;
        private const int REMOVAL_MAX_AGE = 2;

        private readonly MeshRenderer _renderer = renderer;
        private readonly MeshRenderer? _replacement = replacement;

        public MeshRenderer Renderer => _renderer;
        public int Age;

        public bool CanRetire()
        {
            if (_replacement == null)
            {
                return Age >= REMOVAL_MAX_AGE;
            }

            return _replacement.IsBound || _replacement.IsDisposed || Age >= REPLACEMENT_MAX_AGE;
        }

        public void Retire()
        {
            _renderer.Dispose();
            if (!_renderer.Mesh.Shared)
            {
                _renderer.Mesh.Dispose();
            }
        }
    }
}
