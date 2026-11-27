using System.Numerics;

namespace Swordfish.Audio;

/// <summary>Snapshot of the active listener pose and velocity for a single audio tick.</summary>
internal readonly record struct SpatialListenerState(Vector3 Position, Vector3 Right, Vector3 Velocity);
