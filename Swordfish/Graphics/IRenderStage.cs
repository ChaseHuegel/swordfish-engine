using System.Numerics;
using Swordfish.Graphics.SilkNET.OpenGL;

namespace Swordfish.Graphics;

internal interface IRenderStage
{
    void Initialize(IRenderContext renderContext);
    void PreRender(double delta, Matrix4x4 view, Matrix4x4 projection, bool excludeTransparent = false);
    int Render(double delta, Matrix4x4 view, Matrix4x4 projection, Action<ShaderProgram> shaderActivationCallback, bool excludeTransparent = false);
}