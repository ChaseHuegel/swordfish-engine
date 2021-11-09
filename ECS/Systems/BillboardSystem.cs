using OpenTK.Mathematics;
using Swordfish.Rendering;

namespace Swordfish.ECS
{
    [ComponentSystem(typeof(BillboardComponent), typeof(RotationComponent))]
    public class BillboardSystem : ComponentSystem
    {
        public override void OnUpdateEntity(float deltaTime, Entity entity)
        {
            Engine.ECS.Do<RotationComponent>(entity, x =>
            {
                x.orientation = Camera.Main.transform.orientation;
                return x;
            });
        }
    }
}