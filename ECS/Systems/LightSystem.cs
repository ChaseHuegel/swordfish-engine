namespace Swordfish.ECS
{
    [ComponentSystem(typeof(LightComponent), typeof(TransformComponent))]
    public class LightSystem : ComponentSystem
    {
        public override void OnPullEntities() => Engine.Renderer.PushLights(entities);
    }
}