using Silk.NET.OpenGL;
using Swordfish.Settings;

namespace Swordfish.Graphics.SilkNET.OpenGL.Pipelines;

internal sealed class ForwardRenderingPipeline<TRenderStage>(
    in TRenderStage[] renderStages,
    in GL gl,
    in RenderSettings renderSettings
) : RenderPipeline<TRenderStage>(renderStages)
    where TRenderStage : IRenderStage
{
    private readonly GL _gl = gl;
    private readonly RenderSettings _renderSettings = renderSettings;

    public override int PreRender(double delta, RenderScene renderScene)
    {
        AntiAliasing antiAliasing = _renderSettings.AntiAliasing.Get();
        _gl.Set(EnableCap.Multisample, antiAliasing == AntiAliasing.MSAA);
        return 0;
    }

    public override void PostRender(double delta, RenderScene renderScene)
    {
    }

    protected override void ShaderActivationCallback(ShaderProgram shader)
    {
    }
}