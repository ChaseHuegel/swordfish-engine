using Swordfish.Library.Types.Shapes;

namespace Swordfish.ECS;

public readonly struct ColliderComponent : IDataComponent
{
    public readonly Shape? Shape;

    public readonly CompoundShape? CompoundShape;

    public ColliderComponent(Shape shape)
    {
        Shape = shape;
    }

    public ColliderComponent(CompoundShape compoundShape)
    {
        CompoundShape = compoundShape;
    }
}
