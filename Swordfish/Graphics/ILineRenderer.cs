using System.Numerics;
using Swordfish.Graphics.SilkNET.OpenGL;

namespace Swordfish.Graphics;

public interface ILineRenderer
{
    Line CreateLine();

    Line CreateLine(Vector3 start, Vector3 end);

    Line CreateLine(Vector3 start, Vector3 end, Vector4 color);

    void DeleteLine(Line line);
}