using Swordfish.Library.Types;

namespace Swordfish.UI.Elements;

public class DataTreeNode<T> : TreeNode
{
    public DataBinding<T> Data { get; } = new();

    public DataTreeNode(string? name, T data) : base(name)
    {
        Data.Set(data);
    }
}
