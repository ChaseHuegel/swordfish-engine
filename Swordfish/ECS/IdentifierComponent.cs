namespace Swordfish.ECS;

[Component]
public class IdentifierComponent
{
    public string? Name;
    public string? Tag;

    public IdentifierComponent(string? name, string? tag)
    {
        Name = name;
        Tag = tag;
    }
}
