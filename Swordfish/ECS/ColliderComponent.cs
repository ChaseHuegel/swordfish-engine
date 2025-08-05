using Swordfish.Library.Types.Shapes;

namespace Swordfish.ECS;

public struct ColliderComponent : IDataComponent
{
    public readonly Shape? Shape;

    public readonly CompoundShape? CompoundShape;

    internal bool SyncedWithPhysics;

    public ColliderComponent(Shape shape)
    {
        Shape = shape;
    }

    public ColliderComponent(CompoundShape compoundShape)
    {
        CompoundShape = compoundShape;
    }
}
