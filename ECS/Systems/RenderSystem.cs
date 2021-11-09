namespace Swordfish.ECS
{
    [ComponentSystem(typeof(RenderComponent), typeof(PositionComponent))]
    public class RenderSystem : ComponentSystem
    {
        public override void OnPullEntities() => Engine.Renderer.Push(entities);
    }
}