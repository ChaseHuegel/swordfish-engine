namespace Swordfish.ECS;

[Component]
public class IdentifierComponent
{
    public const int DefaultIndex = 0;

    public string? Name;
    public string? Tag;

    public IdentifierComponent() { }

    public IdentifierComponent(string? name)
    {
        Name = name;
    }

    public IdentifierComponent(string? name, string? tag)
    {
        Name = name;
        Tag = tag;
    }
}
