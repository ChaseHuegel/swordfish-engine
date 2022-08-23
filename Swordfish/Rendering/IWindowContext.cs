using Silk.NET.Input;
using Silk.NET.Windowing;

namespace Swordfish.Rendering;

public interface IWindowContext
{
    Action? Load { get; set; }

    Action? Close { get; set; }

    Action<double>? Render { get; set; }

    Action<double>? Update { get; set; }

    void Initialize();
}
