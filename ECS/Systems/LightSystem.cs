namespace Swordfish.ECS
{
    [ComponentSystem(typeof(LightComponent), typeof(PositionComponent))]
    public class LightSystem : ComponentSystem
    {
        public override void OnPullEntities() => Engine.Renderer.PushLights(entities);
    }
}