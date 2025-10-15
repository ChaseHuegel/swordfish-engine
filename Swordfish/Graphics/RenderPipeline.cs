using System.Numerics;

namespace Swordfish.Graphics;

internal abstract class RenderPipeline<TRenderStage> : IRenderPipeline
    where TRenderStage : IRenderStage
{
    private readonly TRenderStage[] _renderStages;
    
    public RenderPipeline(TRenderStage[] renderStages)
    {
        _renderStages = renderStages;
    }
    
    public abstract void PreRender(double delta, Matrix4x4 view, Matrix4x4 projection);

    public abstract void PostRender(double delta, Matrix4x4 view, Matrix4x4 projection);

    public int Render(double delta, Matrix4x4 view, Matrix4x4 projection)
    {
        PreRender(delta, view, projection);
        int drawCalls = Draw(delta, view, projection);
        PostRender(delta, view, projection);
        return drawCalls;
    }
    
    protected int Draw(double delta, Matrix4x4 view, Matrix4x4 projection)
    {
        for (var i = 0; i < _renderStages.Length; i++)
        {
            _renderStages[i].PreRender(delta, view, projection);
        }

        var drawCalls = 0;
        for (var i = 0; i < _renderStages.Length; i++)
        {
            drawCalls += _renderStages[i].Render(delta, view, projection);
        }

        return drawCalls;
    }
}