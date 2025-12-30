using ImGuiNET;
using Microsoft.Extensions.Logging;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using Swordfish.Library.Collections;
using Swordfish.Library.Constraints;
using Swordfish.Library.IO;
using Swordfish.Library.Threading;
using Swordfish.Library.Types;
using Swordfish.UI.Elements;

namespace Swordfish.UI;

internal sealed partial class ImGuiContext : IUIContext
{
    private readonly GL _gl;

    private LockedList<IElement> Elements { get; } = new();

    public DataBinding<IConstraint> ScaleConstraint { get; } = new(new AbsoluteConstraint(1f));
    public DataBinding<float> FontScale { get; } = new(1f);
    public DataBinding<float> FontDisplaySize { get; } = new();

    public ThreadContext ThreadContext { get; }

    private DataBinding<float> Scale { get; } = new(1f);
    private IWindow Window { get; }
    private IInputContext InputContext { get; }
    private IFileParseService FileParseService { get; }
    private ILogger Logger { get; }
    private VirtualFileSystem Vfs { get; }

    public ImGuiContext(IWindow window, IInputContext inputContext, GL gl, IFileParseService fileParseService, ILogger logger, VirtualFileSystem vfs)
    {
        _gl = gl;
        Window = window;
        InputContext = inputContext;
        FileParseService = fileParseService;
        Logger = logger;
        Vfs = vfs;

        ThreadContext = ThreadContext.FromCurrentThread();
        
        OnFontScaleChanged(this, new DataChangedEventArgs<float>(1f, FontScale.Get()));

        Logger.LogInformation("Using ImGui {ImGui}", ImGui.GetVersion());
    }

    public void Initialize()
    {
        ThreadContext.SwitchToCurrentThread();

        Window.Closing += Cleanup;
        Window.Render += Render;

        Scale.Changed += OnFontScaleChanged;
        FontScale.Changed += OnFontScaleChanged;
        ScaleConstraint.Changed += OnScalingConstraintChanged;
    }

    private void OnScalingConstraintChanged(object? sender, DataChangedEventArgs<IConstraint> e)
    {
        Scale.Set(e.NewValue.GetValue(Window?.Monitor?.VideoMode.Resolution?.Y ?? 1f));
    }

    private void OnFontScaleChanged(object? sender, DataChangedEventArgs<float> e)
    {
    }

    private void Cleanup()
    {
    }

    private void Render(double delta)
    {
        ThreadContext.ProcessMessageQueue();
    }

}
