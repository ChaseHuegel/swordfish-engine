using Swordfish.Library.Types.Shapes;

namespace Swordfish.ECS;

[Component]
public class ColliderComponent(IShape shape)
{
    public const int DefaultIndex = 4;

    public readonly IShape Shape = shape;
}
