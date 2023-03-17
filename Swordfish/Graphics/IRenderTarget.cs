using Swordfish.Library.Types;

namespace Swordfish.Graphics;

public interface IRenderTarget
{
    Transform Transform { get; }

    void Render(Camera camera);
}
