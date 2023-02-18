using OpenTK.Mathematics;

namespace Swordfish.Engine.ECS
{
    [ComponentSystem(typeof(TransformComponent), typeof(TurntableComponent))]
    public class TurntableSystem : ComponentSystem
    {
        public override void OnUpdateEntity(float deltaTime, Entity entity)
        {
            Swordfish.ECS.Do<TransformComponent>(entity, x =>
            {
                // x.Rotate(Vector3.UnitY, 45 * deltaTime);
                x.orientation = Quaternion.FromEulerAngles(0f, Swordfish.Time * 0.005f * 360f, 0f);
                return x;
            });
        }
    }
}