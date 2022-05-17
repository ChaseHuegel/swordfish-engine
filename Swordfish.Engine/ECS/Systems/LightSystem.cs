namespace Swordfish.Engine.ECS
{
    [ComponentSystem(typeof(LightComponent), typeof(TransformComponent))]
    public class LightSystem : ComponentSystem
    {
        public override void OnPullEntities() => Swordfish.Renderer.PushLights(entities);
    }
}