using System.Numerics;

namespace WaywardBeyond.Client.Core.Events;

internal readonly struct PlayerMovedEvent(Vector3 position)
{
    public readonly Vector3 Position = position;
}