using OpenTK.Mathematics;
using Swordfish.Rendering;

namespace Swordfish.ECS
{
    [ComponentSystem(typeof(BillboardComponent), typeof(RotationComponent))]
    public class BillboardSystem : ComponentSystem
    {
        public override void OnUpdate(float deltaTime)
        {
            foreach (Entity entity in entities)
            {
                Engine.ECS.Do<RotationComponent>(entity, x =>
                {
                    x.orientation = Camera.Main.transform.orientation;
                    return x;
                });
            }
        }
    }
}