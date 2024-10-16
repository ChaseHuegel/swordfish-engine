using System.Numerics;
using Swordfish.Graphics.SilkNET.OpenGL;

namespace Swordfish.Graphics;

public interface ILineRenderer
{
    Line CreateLine(bool alwaysOnTop = false);

    Line CreateLine(Vector3 start, Vector3 end, bool alwaysOnTop = false);

    Line CreateLine(Vector3 start, Vector3 end, Vector4 color, bool alwaysOnTop = false);

    void DeleteLine(Line line);
}