using Swordfish.Graphics.SilkNET.OpenGL;

namespace Swordfish.Graphics;

internal interface IRenderStage
{
    void PreRender(double delta, RenderScene renderScene, bool excludeTransparent = false);
    int Render(double delta, RenderScene renderScene, Action<ShaderProgram> shaderActivationCallback, bool excludeTransparent = false);
}