namespace Swordfish.Engine.ECS
{
    [ComponentSystem(typeof(RenderComponent), typeof(TransformComponent))]
    public class RenderSystem : ComponentSystem
    {
        public override void OnPullEntities() => Swordfish.Renderer.Push(entities);
    }
}