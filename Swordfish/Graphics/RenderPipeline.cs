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
    
    public abstract void PreRender(double delta, RenderScene renderScene);

    public abstract void PostRender(double delta, RenderScene renderScene);

    public int Render(double delta, RenderScene renderScene)
    {
        PreRender(delta, renderScene);
        int drawCalls = Draw(delta, renderScene);
        PostRender(delta, renderScene);
        return drawCalls;
    }
    
    protected int Draw(double delta, RenderScene renderScene, bool isDepthPass = false)
    {
        PreRenderStages(delta, renderScene, isDepthPass);

        var drawCalls = 0;
        for (var i = 0; i < _renderStages.Length; i++)
        {
            drawCalls += _renderStages[i].Render(delta, renderScene, ShaderActivationCallback, isDepthPass);
        }

        return drawCalls;
    }

    protected void PreRenderStages(double delta, RenderScene renderScene, bool isDepthPass = false)
    {
        for (var i = 0; i < _renderStages.Length; i++)
        {
            _renderStages[i].PreRender(delta, renderScene, isDepthPass);
        }
    }

    protected int DrawShadowPass(
        Matrix4x4 view,
        Matrix4x4 projection,
        ShaderProgram depthShader,
        ShaderProgram depthCutoutShader,
        ShaderProgram depthCutoutArrayShader
    ) {
        var drawCalls = 0;
        for (var i = 0; i < _renderStages.Length; i++)
        {
            drawCalls += _renderStages[i].RenderShadowPass(view, projection, depthShader, depthCutoutShader, depthCutoutArrayShader);
        }

        return drawCalls;
    }

    protected abstract void ShaderActivationCallback(ShaderProgram shader);
}