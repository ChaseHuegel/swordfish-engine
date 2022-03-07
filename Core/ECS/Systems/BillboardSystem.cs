using Swordfish.Core.Rendering;

namespace Swordfish.Core.ECS
{
    [ComponentSystem(typeof(BillboardComponent), typeof(TransformComponent))]
    public class BillboardSystem : ComponentSystem
    {
        public override void OnUpdateEntity(float deltaTime, Entity entity)
        {
            Engine.ECS.Do<TransformComponent>(entity, x =>
            {
                x.orientation = Camera.Main.transform.orientation;
                return x;
            });
        }
    }
}