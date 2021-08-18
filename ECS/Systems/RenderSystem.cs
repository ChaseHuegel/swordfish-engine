namespace Swordfish.ECS
{
    [ComponentSystem(typeof(RenderComponent), typeof(PositionComponent))]
    public class RenderSystem : ComponentSystem
    {
        public override void OnEntityUpdate() => Engine.Renderer.Push(entities);
    }
}