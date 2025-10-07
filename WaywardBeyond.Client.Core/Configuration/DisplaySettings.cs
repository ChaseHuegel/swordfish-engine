using Swordfish.Graphics;
using Swordfish.Library.Types;

namespace WaywardBeyond.Client.Core.Configuration;

internal sealed class DisplaySettings
{
    public DataBinding<bool> Fullscreen { get; } = new();

    private readonly IWindowContext _windowContext;
    
    public DisplaySettings(in IWindowContext windowContext)
    {
        _windowContext = windowContext;

        Fullscreen.Changed += OnFullscreenChanged;
    }

    private void OnFullscreenChanged(object? sender, DataChangedEventArgs<bool> e)
    {
        if (e.NewValue)
        {
            _windowContext.Fullscreen();
        }
        else
        {
            _windowContext.Maximize();
        }
    }
}