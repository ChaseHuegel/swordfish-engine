using System.Numerics;

namespace Swordfish.Graphics;

public interface IWindowContext
{
    Vector2 MonitorResolution { get; }

    Action? Loaded { get; set; }
    Action? Closed { get; set; }
    Action<double>? Render { get; set; }
    Action<double>? Update { get; set; }

    void Initialize();

    Vector2 GetSize();

    void Close();
    void SetWindowed();
    void Minimize();
    void Maximize();
    void Fullscreen();
}
