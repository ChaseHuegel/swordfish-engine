using Swordfish.Library.Types.Shapes;

namespace Swordfish.ECS;

public readonly struct ColliderComponent(IShape shape) : IDataComponent
{
    public readonly IShape Shape = shape;
}
