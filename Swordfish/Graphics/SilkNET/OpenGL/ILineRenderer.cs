using System.Numerics;

namespace Swordfish.Graphics.SilkNET.OpenGL;

public interface ILineRenderer
{
    Line CreateLine();

    Line CreateLine(Vector3 start, Vector3 end);

    Line CreateLine(Vector3 start, Vector3 end, Vector4 color);

    void DeleteLine(Line line);
}