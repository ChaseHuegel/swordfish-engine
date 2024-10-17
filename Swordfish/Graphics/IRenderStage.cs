using System.Numerics;
using Swordfish.Graphics;

public interface IRenderStage
{
    void Initialize(IRenderContext renderContext);
    void PreRender(double delta, Matrix4x4 view, Matrix4x4 projection);
    int Render(double delta, Matrix4x4 view, Matrix4x4 projection);
}