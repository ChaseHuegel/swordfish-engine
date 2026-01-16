namespace Swordfish.Graphics;

internal interface IRenderPipeline
{
    int Render(double delta, RenderScene renderScene);
}