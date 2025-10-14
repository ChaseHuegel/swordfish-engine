using System.Numerics;

namespace Swordfish.Graphics;

internal interface IRenderPipeline
{
    int Render(double delta, Matrix4x4 view, Matrix4x4 projection);
}