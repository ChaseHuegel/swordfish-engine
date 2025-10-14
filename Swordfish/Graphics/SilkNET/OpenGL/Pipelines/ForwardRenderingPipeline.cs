using System.Numerics;
using Silk.NET.OpenGL;
using Swordfish.Settings;

namespace Swordfish.Graphics.SilkNET.OpenGL.Pipelines;

internal sealed class ForwardRenderingPipeline<TRenderStage> : RenderPipeline<TRenderStage> 
    where TRenderStage : IForwardRenderStage
{
    private readonly GL _gl;
    private readonly RenderSettings _renderSettings;

    public ForwardRenderingPipeline(in TRenderStage[] renderStages, in GL gl, in RenderSettings renderSettings) : base(renderStages)
    {
        _gl = gl;
        _renderSettings = renderSettings;
    }

    public override void PreRender(double delta, Matrix4x4 view, Matrix4x4 projection)
    {
        AntiAliasing antiAliasing = _renderSettings.AntiAliasing.Get();
        _gl.Set(EnableCap.Multisample, antiAliasing == AntiAliasing.MSAA);
    }

    public override void PostRender(double delta, Matrix4x4 view, Matrix4x4 projection)
    {
    }
}