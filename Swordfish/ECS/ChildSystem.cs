using System.Numerics;

namespace Swordfish.ECS;

public class ChildSystem : IEntitySystem
{
    public void Tick(float delta, DataStore store)
    {
        UpdateChildAction action = default;
        store.QueryRef<ChildComponent, TransformComponent, UpdateChildAction>(delta, ref action);
    }
    
    private readonly struct UpdateChildAction : IForEachRef<ChildComponent, TransformComponent>
    {
        public void Execute(float delta, DataStore store, int entity, ref Ref<ChildComponent> child, ref Ref<TransformComponent> transform)
        {
            if (!store.TryGet(child.Read.Parent, out TransformComponent parentTransform))
            {
                return;
            }

            transform.Write.Position = parentTransform.Position + Vector3.Transform(child.Read.LocalPosition, parentTransform.Orientation);
            transform.Write.Orientation = parentTransform.Orientation * child.Read.LocalOrientation;
            transform.Write.Scale = parentTransform.Scale * child.Read.LocalScale;
        }
    }
}