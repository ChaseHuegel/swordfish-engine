using System.Numerics;

namespace Swordfish.ECS;

public class ChildSystem : EntitySystem<ChildComponent, TransformComponent>
{
    public override int Order => -100_000;
    
    protected override void OnTick(float delta, DataStore store, int entity, ref ChildComponent child, ref TransformComponent transform)
    {
        if (!store.TryGet(child.Parent, out TransformComponent parentTransform))
        {
            return;
        }

        transform.Position = parentTransform.Position + Vector3.Transform(child.LocalPosition, parentTransform.Orientation);
        transform.Orientation = parentTransform.Orientation * child.LocalOrientation;
        transform.Scale = parentTransform.Scale * child.LocalScale;
    }
}
