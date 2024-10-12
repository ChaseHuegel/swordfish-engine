using JoltPhysicsSharp;

namespace Swordfish.Physics.Jolt;

internal interface IJoltPhysics
{
    PhysicsSystem System { get; }
}