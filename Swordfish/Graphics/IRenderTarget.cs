using Swordfish.Library.Types;

namespace Swordfish.Graphics;

public interface IRenderTarget
{
    Transform Transform { get; set; }

    void Dispose();

    void Render(Camera camera);
}
