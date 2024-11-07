namespace Swordfish.ECS;

public struct IdentifierComponent : IDataComponent
{
    public string? Name;
    public string? Tag;

    // ReSharper disable once UnusedMember.Global
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
