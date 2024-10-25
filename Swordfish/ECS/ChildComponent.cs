namespace Swordfish.ECS;

public struct ChildComponent : IDataComponent
{
    public int Parent;

    public ChildComponent(int parent)
    {
        Parent = parent;
    }
}