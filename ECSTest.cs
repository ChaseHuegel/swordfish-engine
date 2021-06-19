using OpenTK.Mathematics;

using Swordfish;
using Swordfish.ECS;

namespace ECSTest
{
    [ComponentSystem(typeof(RotationComponent))]
    public class RotateSystem : ComponentSystem
    {
        public override void OnUpdate(float deltaTime)
        {
            foreach (Entity entity in entities)
            {
                Engine.ECS.Do<RotationComponent>(entity, x =>
                {
                    x.Rotate(Vector3.UnitY, 45 * deltaTime);
                    return x;
                });
            }
        }
    }
}