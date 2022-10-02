namespace Swordfish.ECS;

[Component]
public partial class IdentifierComponent
{
    public const int DefaultIndex = 0;

    public string? Name;
    public string? Tag;

    public IdentifierComponent(string? name, string? tag)
    {
        Name = name;
        Tag = tag;
    }
}
