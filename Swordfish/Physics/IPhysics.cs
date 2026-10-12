using System.Numerics;

namespace Swordfish.Physics;

public interface IPhysics
{
    /// <summary>The fixed timestep the solver runs at, in seconds.</summary>
    float FixedTimestamp { get; }

    event EventHandler<EventArgs>? FixedUpdate;

    RaycastResult Raycast(in Ray ray);
    
    void SetGravity(Vector3 gravity);
}
