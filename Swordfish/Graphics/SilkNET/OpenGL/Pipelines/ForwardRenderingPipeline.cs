using System.Numerics;
using Silk.NET.OpenGL;
using Swordfish.Graphics.SilkNET.OpenGL.Renderers;
using Swordfish.Settings;

namespace Swordfish.Graphics.SilkNET.OpenGL.Pipelines;

internal sealed class ForwardRenderingPipeline<TRenderStage> : RenderPipeline<TRenderStage> 
    where TRenderStage : IRenderStage
{
    private readonly GL _gl;
    private readonly RenderSettings _renderSettings;

    public ForwardRenderingPipeline(in TRenderStage[] renderStages, in GL gl, in RenderSettings renderSettings) : base(renderStages)
    {
        _gl = gl;
        _renderSettings = renderSettings;
    }

    public override void PreRender(double delta, Matrix4x4 view, Matrix4x4 projection, RenderInstance[] renderInstances)
    {
        AntiAliasing antiAliasing = _renderSettings.AntiAliasing.Get();
        _gl.Set(EnableCap.Multisample, antiAliasing == AntiAliasing.MSAA);
    }

    public override void PostRender(double delta, Matrix4x4 view, Matrix4x4 projection, RenderInstance[] renderInstances)
    {
    }

    protected override void ShaderActivationCallback(ShaderProgram shader)
    {
    }
}