using System.Globalization;
using System.Runtime.InteropServices;
using ImGuiNET;
using Microsoft.Extensions.Logging;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.Collections;
using Swordfish.Library.Constraints;
using Swordfish.Library.IO;
using Swordfish.Library.Threading;
using Swordfish.Library.Types;
using Swordfish.UI.Elements;
using Tomlet;

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
    private ImGuiController Controller { get; }
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
        
        Controller = new ImGuiController(_gl, Window, InputContext, ConfigureImGuiIO);
        Controller.Update(0f);

        OnFontScaleChanged(this, new DataChangedEventArgs<float>(1f, FontScale.Get()));

        Logger.LogInformation("Using ImGui {ImGui}", ImGui.GetVersion());
    }

    private void ConfigureImGuiIO()
    {
        LoadFontsFromDisk();
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
        ImGui.GetIO().FontGlobalScale = FontScale.Get() * e.NewValue;
        FontDisplaySize.Set(ImGui.GetFontSize() * FontScale.Get() * e.NewValue);
    }

    private void Cleanup()
    {
        Controller.Dispose();
    }

    private void Render(double delta)
    {
        Controller.Update((float)delta);

        foreach (IElement element in Elements.ToArray())
        {
            element.Render();
        }

        Controller.Render();

        ThreadContext.ProcessMessageQueue();
    }

    private void LoadFontsFromDisk()
    {
        List<Font> fonts = [];
        Dictionary<string, PathInfo> fontFiles = new();

        PathInfo[] files = Vfs.GetFiles(AssetPaths.Fonts, SearchOption.AllDirectories);

        PathInfo[] configFiles = files.Where(file => file.GetExtension() == ".toml").ToArray();

        foreach (PathInfo path in files.Where(file => file.GetExtension() == ".otf"))
        {
            fontFiles.TryAdd(path.GetFileNameWithoutExtension(), path);
        }

        foreach (PathInfo path in files.Where(file => file.GetExtension() == ".ttf"))
        {
            fontFiles.TryAdd(path.GetFileNameWithoutExtension(), path);
        }

        Logger.LogInformation("Loading fonts from {fontCount} files and {configCount} configs...", fontFiles.Count, configFiles.Length);

        foreach (PathInfo configFile in configFiles)
        {
            string fontName = configFile.GetFileNameWithoutExtension();

            //  Check there is a matching font file for this config
            if (!fontFiles.TryGetValue(fontName, out PathInfo fontFile))
            {
                continue;
            }

            string content = configFile.ReadString();
            var font = TomletMain.To<Font>(content);
            font.Name = fontName;
            font.Source = fontFile;

            if (font.IsDefault)
            {
                fonts.Insert(0, font);
            }
            else
            {
                fonts.Add(font);
            }
        }

        if (fonts.Count == 0 || !fonts[0].IsDefault)
        {
            ImGui.GetIO().Fonts.AddFontDefault();
        }

        foreach (Font font in fonts)
        {
            LoadFont(
                font.Source,
                font.Size,
                font.IsIcons,
                (
                    ushort.Parse(font.MinUnicode, NumberStyles.HexNumber),
                    ushort.Parse(font.MaxUnicode, NumberStyles.HexNumber)
                )
            );

            Logger.LogInformation("Loaded font '{Name}'. Size: {Size} Unicode: {MinUnicode}-{MaxUnicode} Icons: {IsIcons} Default: {IsDefault}", font.Name, font.Size, font.MinUnicode, font.MaxUnicode, font.IsIcons, font.IsDefault);
        }

        Logger.LogInformation("Loaded {count} fonts", fonts.Count);
    }

    private static unsafe void LoadFont(PathInfo fontFile, float fontSize, bool mergeMode, (ushort?, ushort?) charRange)
    {
        ImFontConfigPtr config = ImGuiNative.ImFontConfig_ImFontConfig();

        config.MergeMode = mergeMode;
        config.PixelSnapH = true;

        IntPtr charRangePtr;
        GCHandle charRangeHandle = default;

        if (charRange.Item1 == null || charRange.Item2 == null)
        {
            charRangePtr = ImGui.GetIO().Fonts.GetGlyphRangesDefault();
        }
        else
        {
            charRangeHandle = GCHandle.Alloc(new ushort[] {
                charRange.Item1.Value,
                charRange.Item2.Value,
                0,
            }, GCHandleType.Pinned);

            charRangePtr = charRangeHandle.AddrOfPinnedObject();
        }


        try
        {
            ImGui.GetIO().Fonts.AddFontFromFileTTF(fontFile.Value, fontSize, config, charRangePtr);
        }
        finally
        {
            config.Destroy();

            if (charRangeHandle.IsAllocated)
            {
                charRangeHandle.Free();
            }
        }
    }

}
