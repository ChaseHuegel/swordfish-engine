using Swordfish.Library.Types;

namespace Swordfish.Graphics;

public interface IRenderer
{
    DataBinding<int> DrawCalls { get; }

    Texture Screenshot();
}
