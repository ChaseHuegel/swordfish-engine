using Swordfish.Library.Types.Shapes;

namespace Swordfish.ECS;

public struct ColliderComponent : IDataComponent
{
    public IShape Shape;

    public ColliderComponent(IShape shape)
    {
        Shape = shape;
    }
}
