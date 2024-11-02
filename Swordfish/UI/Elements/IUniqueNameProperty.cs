namespace Swordfish.UI.Elements;

public interface IUniqueNameProperty : INameProperty, IUIDProperty
{
    string UniqueName { get; }
}
