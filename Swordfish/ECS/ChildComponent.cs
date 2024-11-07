namespace Swordfish.ECS;

public struct ChildComponent(in int parent) : IDataComponent
{
    public int Parent = parent;
}