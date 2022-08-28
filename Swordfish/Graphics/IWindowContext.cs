using System.Numerics;

namespace Swordfish.Graphics;

public interface IWindowContext
{
    Action? Load { get; set; }
    Action? Close { get; set; }
    Action<double>? Render { get; set; }
    Action<double>? Update { get; set; }

    void Initialize();

    Vector2 GetSize();

    void SetWindowed();
    void Minimize();
    void Maximize();
    void Fullscreen();
}
