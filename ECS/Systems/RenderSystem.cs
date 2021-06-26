namespace Swordfish.ECS
{
    [ComponentSystem(typeof(RenderComponent), typeof(RotationComponent), typeof(PositionComponent))]
    public class RenderSystem : ComponentSystem
    {
        public override void OnEntityUpdate() => Engine.Renderer.Push(entities);
    }
}