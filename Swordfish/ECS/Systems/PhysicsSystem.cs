namespace Swordfish.ECS
{
    [ComponentSystem(typeof(RigidbodyComponent), typeof(CollisionComponent), typeof(PositionComponent), typeof(RotationComponent))]
    public class PhysicsSystem : ComponentSystem
    {
        public override void OnEntityUpdate() => Engine.Physics.Push(entities);
    }
}