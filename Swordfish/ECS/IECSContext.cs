using Swordfish.Library.Types;

namespace Swordfish.ECS;

public interface IECSContext
{
    DataBinding<double> Delta { get; }
    World World { get; }

    void Start();

    void Update(float delta);
}