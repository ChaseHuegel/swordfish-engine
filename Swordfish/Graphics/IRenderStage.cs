using System.Numerics;

public interface IRenderStage
{
    void Load();

    int Render(double delta, Matrix4x4 view, Matrix4x4 projection);
}