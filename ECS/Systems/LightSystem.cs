namespace Swordfish.ECS
{
    [ComponentSystem(typeof(LightComponent), typeof(PositionComponent))]
    public class LightSystem : ComponentSystem
    {
        public override void OnEntityUpdate() => Engine.Renderer.PushLights(entities);
    }
}