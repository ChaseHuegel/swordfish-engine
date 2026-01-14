using System.Numerics;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Graphics.SilkNET.OpenGL.Renderers;

namespace Swordfish.Graphics;

internal abstract class RenderPipeline<TRenderStage> : IRenderPipeline
    where TRenderStage : IRenderStage
{
    private readonly TRenderStage[] _renderStages;
    
    public RenderPipeline(TRenderStage[] renderStages)
    {
        _renderStages = renderStages;
    }
    
    public abstract void PreRender(double delta, Matrix4x4 view, Matrix4x4 projection, RenderInstance[] renderInstances);

    public abstract void PostRender(double delta, Matrix4x4 view, Matrix4x4 projection, RenderInstance[] renderInstances);

    public int Render(double delta, Matrix4x4 view, Matrix4x4 projection, RenderInstance[] renderInstances)
    {
        PreRender(delta, view, projection, renderInstances);
        int drawCalls = Draw(delta, view, projection, renderInstances);
        PostRender(delta, view, projection, renderInstances);
        return drawCalls;
    }
    
    protected int Draw(double delta, Matrix4x4 view, Matrix4x4 projection, RenderInstance[] renderInstances, bool isDepthPass = false)
    {
        for (var i = 0; i < _renderStages.Length; i++)
        {
            _renderStages[i].PreRender(delta, view, projection, renderInstances, isDepthPass);
        }

        var drawCalls = 0;
        for (var i = 0; i < _renderStages.Length; i++)
        {
            drawCalls += _renderStages[i].Render(delta, view, projection, renderInstances, ShaderActivationCallback, isDepthPass);
        }

        return drawCalls;
    }

    protected abstract void ShaderActivationCallback(ShaderProgram shader);
}