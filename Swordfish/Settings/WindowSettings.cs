using Swordfish.Library.Configuration;
using Swordfish.Library.Types;

namespace Swordfish.Settings;

public sealed class WindowSettings : Config<WindowSettings>
{
    public DataBinding<string> Title { get; private set; } = new(string.Empty);
    
    public DataBinding<int?> X { get; private set; } = new();
    public DataBinding<int?> Y { get; private set; } = new();
    
    public DataBinding<int?> Width { get; private set; } = new();
    public DataBinding<int?> Height { get; private set; } = new();
    
    public DataBinding<WindowMode> Mode { get; private set; } = new();
    
    public DataBinding<bool> Borderless { get; private set; } = new();

    public DataBinding<bool> AllowResize { get; private set; } = new(true);
    
    public DataBinding<bool> AlwaysOnTop { get; private set; } = new();
}