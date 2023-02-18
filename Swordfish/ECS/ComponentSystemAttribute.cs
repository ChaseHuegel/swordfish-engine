namespace Swordfish.ECS;

[AttributeUsage(AttributeTargets.Class)]
public class ComponentSystemAttribute : Attribute
{
    public Type[] Filter;

    public ComponentSystemAttribute(params Type[] filter)
    {
        Filter = filter;
    }
}
