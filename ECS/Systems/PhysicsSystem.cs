namespace Swordfish.ECS
{
    [ComponentSystem(typeof(RigidbodyComponent), typeof(PositionComponent), typeof(RotationComponent))]
    public class PhysicsSystem : ComponentSystem
    {
        public override void OnEntityUpdate() => Engine.Physics.PushBodies(entities);
    }
}