using System.Numerics;

namespace Swordfish.Physics;

public interface IPhysics
{
    /// <summary>The fixed timestep the solver runs at, in seconds.</summary>
    float FixedTimestamp { get; }

    event EventHandler<EventArgs>? FixedUpdate;

    /// <summary>Raised on the physics tick thread after a solver step for each resolved contact.</summary>
    event Action<PhysicsContactEvent>? ContactResolved
    {
        add { }
        remove { }
    }

    RaycastResult Raycast(in Ray ray);

    /// <summary>Casts <paramref name="ray"/> against physics bodies while ignoring <paramref name="ignoreEntity"/> when non-negative.</summary>
    RaycastResult Raycast(in Ray ray, int ignoreEntity) => Raycast(in ray);

    /// <summary>Returns whether any physics body overlaps the queried shape beyond its penetration threshold.</summary>
    bool Overlaps(in ShapeOverlapQuery query);
    
    void SetGravity(Vector3 gravity);
}
