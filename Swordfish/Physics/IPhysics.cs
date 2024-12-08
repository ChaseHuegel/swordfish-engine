using System.Numerics;

namespace Swordfish.Physics;

public interface IPhysics
{
    event EventHandler<EventArgs>? FixedUpdate;

    RaycastResult Raycast(in Ray ray);
    
    void SetGravity(Vector3 gravity);
}
