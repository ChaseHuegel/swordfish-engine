namespace Swordfish.ECS;

public class ChildSystem : EntitySystem<ChildComponent, TransformComponent>
{
    protected override void OnTick(float delta, DataStore store, int entity, ref ChildComponent child, ref TransformComponent transform)
    {
        if (!store.TryGet(child.Parent, out TransformComponent parentTransform))
        {
            return;
        }

        //  TODO support local offsets
        transform.Position = parentTransform.Position;
        transform.Orientation = parentTransform.Orientation;
        transform.Scale = parentTransform.Scale;
    }
}
