namespace Swordfish.ECS
{
    [ComponentSystem(typeof(RenderComponent), typeof(TransformComponent))]
    public class RenderSystem : ComponentSystem
    {
        public override void OnPullEntities() => Engine.Renderer.Push(entities);
    }
}