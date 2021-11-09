namespace Swordfish.ECS
{
    [ComponentSystem(typeof(CollisionComponent), typeof(PositionComponent), typeof(RotationComponent))]
    public class CollisionSystem : ComponentSystem
    {
        public override void OnPullEntities() => Engine.Physics.PushColliders(entities);
    }
}