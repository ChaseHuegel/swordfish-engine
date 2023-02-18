namespace Swordfish.Engine.ECS
{
    [ComponentSystem(typeof(RigidbodyComponent), typeof(TransformComponent))]
    public class PhysicsSystem : ComponentSystem
    {
        public override void OnPullEntities() => Swordfish.Physics.PushBodies(entities);
    }
}