namespace Swordfish.ECS
{
    [ComponentSystem(typeof(CollisionComponent), typeof(TransformComponent))]
    public class CollisionSystem : ComponentSystem
    {
        public override void OnPullEntities() => Engine.Physics.PushColliders(entities);
    }
}