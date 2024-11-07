namespace Swordfish.Physics;

public interface IPhysics
{
    event EventHandler<EventArgs>? FixedUpdate;

    RaycastResult Raycast(in Ray ray);
}
