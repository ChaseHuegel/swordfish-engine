using System.Numerics;

namespace Swordfish.Physics;

public interface IPhysics
{
    /// <summary>The fixed timestep the solver runs at, in seconds.</summary>
    float FixedTimestamp { get; }

    event EventHandler<EventArgs>? FixedUpdate;

    RaycastResult Raycast(in Ray ray);

    /// <summary>Returns whether any physics body overlaps the queried shape beyond its penetration threshold.</summary>
    bool Overlaps(in ShapeOverlapQuery query);
    
    void SetGravity(Vector3 gravity);
}
