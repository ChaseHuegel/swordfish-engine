using Swordfish.Graphics.SilkNET;

namespace Swordfish.Graphics;

public interface IRenderContext
{
    void Initialize();

    void Render(double delta);

    void Bind(IRenderTarget renderTarget);
}
