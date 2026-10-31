using System.Numerics;
using Swordfish.Graphics.SilkNET.OpenGL;

namespace Swordfish.Graphics;

internal interface IRenderStage
{
    void PreRender(double delta, RenderScene renderScene, bool excludeTransparent = false);
    int Render(double delta, RenderScene renderScene, Action<ShaderProgram> shaderActivationCallback, bool excludeTransparent = false);
    int RenderShadowPass(
        Matrix4x4 view,
        Matrix4x4 projection,
        ShaderProgram depthShader,
        ShaderProgram depthCutoutShader,
        ShaderProgram depthCutoutArrayShader
    ) => 0;

    /// <summary>Supplies the depth shaders used by depth and shadow passes.</summary>
    void SetDepthShaders(ShaderProgram depthShader, ShaderProgram depthCutoutShader, ShaderProgram depthCutoutArrayShader) { }
}
