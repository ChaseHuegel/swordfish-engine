namespace Swordfish.UI.Elements;

public interface IUniqueNameProperty : INameProperty, IUidProperty
{
    string UniqueName { get; }
}
