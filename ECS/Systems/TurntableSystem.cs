using OpenTK.Mathematics;
using Swordfish.Rendering;

namespace Swordfish.ECS
{
    [ComponentSystem(typeof(TransformComponent), typeof(TurntableComponent))]
    public class TurntableSystem : ComponentSystem
    {
        public override void OnUpdateEntity(float deltaTime, Entity entity)
        {
            Engine.ECS.Do<TransformComponent>(entity, x =>
            {
                // x.Rotate(Vector3.UnitY, 45 * deltaTime);
                x.orientation = Quaternion.FromEulerAngles(0f, Engine.Time * 0.005f * 360f, 0f);
                return x;
            });
        }
    }
}