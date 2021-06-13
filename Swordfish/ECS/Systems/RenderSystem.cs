namespace Swordfish.ECS
{
    [ComponentSystem(typeof(PositionComponent), typeof(RotationComponent), typeof(RenderComponent))]
    public class RenderSystem : ComponentSystem
    {
        public override void OnEntityUpdate() => Engine.Renderer.PushEntities(entities);
    }
}