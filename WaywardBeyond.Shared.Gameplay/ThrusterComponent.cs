using Swordfish.ECS;

namespace WaywardBeyond.Shared.Gameplay;

/// <summary>
/// Marks a dynamic structure (or other body) as producing a constant forward thrust along its local +Z
/// axis, applied per fixed physics step by <see cref="SharedPlayerMotionStep"/>. Both the server authority
/// and client prediction worlds evaluate it identically, so structure motion driven by thrusters is
/// deterministic across worlds and corrected by authoritative snapshots.
/// </summary>
public struct ThrusterComponent(in int power) : IDataComponent
{
    public int Power = power;
}