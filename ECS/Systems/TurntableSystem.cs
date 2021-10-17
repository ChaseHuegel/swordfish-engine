using OpenTK.Mathematics;
using Swordfish.Rendering;

namespace Swordfish.ECS
{
    [ComponentSystem(typeof(RotationComponent), typeof(TurntableComponent))]
    public class TurntableSystem : ComponentSystem
    {
        public override void OnUpdate(float deltaTime)
        {
            foreach (Entity entity in entities)
            {
                Engine.ECS.Do<RotationComponent>(entity, x =>
                {
                    // x.Rotate(Vector3.UnitY, 45 * deltaTime);
                    x.orientation = Quaternion.FromEulerAngles(0f, Engine.Time * 0.005f * 360f, 0f);
                    return x;
                });
            }
        }
    }
}