using System.Numerics;
using Swordfish.Graphics.SilkNET.OpenGL.Renderers;

namespace Swordfish.Graphics;

internal interface IRenderPipeline
{
    int Render(double delta, Matrix4x4 view, Matrix4x4 projection, RenderInstance[] renderInstances);
}