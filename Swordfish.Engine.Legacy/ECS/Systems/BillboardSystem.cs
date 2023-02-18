using Swordfish.Engine.Rendering;

namespace Swordfish.Engine.ECS
{
    [ComponentSystem(typeof(BillboardComponent), typeof(TransformComponent))]
    public class BillboardSystem : ComponentSystem
    {
        public override void OnUpdateEntity(float deltaTime, Entity entity)
        {
            Swordfish.ECS.Do<TransformComponent>(entity, x =>
            {
                x.orientation = Camera.Main.transform.orientation;
                return x;
            });
        }
    }
}