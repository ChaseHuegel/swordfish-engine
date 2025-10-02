using System.Numerics;
using Swordfish.Library.Types;

namespace Swordfish.Graphics;

public interface IWindowContext
{
    DataBinding<double> UpdateDelta { get; }
    DataBinding<double> RenderDelta { get; }

    Vector2 Resolution { get; }
    Vector2 MonitorResolution { get; }

    Action? Loaded { get; set; }
    Action? Closed { get; set; }
    Action<double>? Render { get; set; }
    Action<double>? Update { get; set; }
    Action? Focused { get; set; }
    Action? Unfocused { get; set; }
    Action<Vector2>? Resized { get; set; }

    Vector2 GetSize();

    void Close();
    void SetWindowed();
    void Minimize();
    void Maximize();
    void Fullscreen();
    void SetTitle(string? title);
    void SetIcon(Texture icon);
}