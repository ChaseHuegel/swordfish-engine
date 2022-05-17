namespace Swordfish.Engine.ECS
{
    [ComponentSystem(typeof(CollisionComponent), typeof(TransformComponent))]
    public class CollisionSystem : ComponentSystem
    {
        public override void OnPullEntities() => Swordfish.Physics.PushColliders(entities);
    }
}