using System.Numerics;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Graphics.SilkNET.OpenGL.Renderers;

namespace Swordfish.Graphics;

internal interface IRenderStage
{
    void Initialize(IRenderer renderer);
    void PreRender(double delta, Matrix4x4 view, Matrix4x4 projection, RenderInstance[] renderInstances, bool excludeTransparent = false);
    int Render(double delta, Matrix4x4 view, Matrix4x4 projection, RenderInstance[] renderInstances, Action<ShaderProgram> shaderActivationCallback, bool excludeTransparent = false);
}