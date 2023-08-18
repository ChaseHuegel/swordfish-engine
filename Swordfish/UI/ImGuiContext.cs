using System.Globalization;
using System.Runtime.InteropServices;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using Swordfish.Library.Collections;
using Swordfish.Library.Constraints;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using Swordfish.UI.Elements;
using Tomlet;

namespace Swordfish.UI;

public class ImGuiContext : IUIContext
{
    private GL GL;

    public LockedList<IElement> Elements { get; } = new();
    public IMenuBarElement? MenuBar { get; set; }

    public DataBinding<IConstraint> ScaleConstraint { get; set; } = new DataBinding<IConstraint>(new AbsoluteConstraint(1f));
    public DataBinding<float> FontScale { get; } = new DataBinding<float>(1f);
    public DataBinding<float> FontDisplaySize { get; } = new DataBinding<float>();

    private DataBinding<float> Scale { get; } = new DataBinding<float>(1f);
    private ImGuiController Controller { get; set; }
    private IWindow Window { get; set; }
    private IInputContext InputContext { get; set; }
    private IFileService FileService { get; set; }
    private IPathService PathService { get; set; }

    public ImGuiContext(IWindow window, Silk.NET.Input.IInputContext inputContext, GL gl, IFileService fileService, IPathService pathService)
    {
        GL = gl;
        Window = window;
        InputContext = inputContext;
        FileService = fileService;
        PathService = pathService;

        Controller = new ImGuiController(GL, Window, InputContext, ConfigureImGuiIO);
        Controller.Update(0f);

        OnFontScaleChanged(this, EventArgs.Empty);

        Debugger.Log("UI initialized.");
        Debugger.Log($"using ImGui {ImGui.GetVersion()}", LogType.CONTINUED);
    }

    private void ConfigureImGuiIO()
    {
        LoadFontsFromDisk();
    }

    public void Initialize()
    {
        Window.Closing += Cleanup;
        Window.Render += Render;

        Scale.Changed += OnFontScaleChanged;
        FontScale.Changed += OnFontScaleChanged;
        ScaleConstraint.Changed += OnScalingConstraintChanged;
    }

    private void OnScalingConstraintChanged(object? sender, EventArgs e)
    {
        Scale.Set(ScaleConstraint.Get().GetValue(Window?.Monitor?.VideoMode.Resolution?.Y ?? 1f));
    }

    private void OnFontScaleChanged(object? sender, EventArgs e)
    {
        ImGui.GetIO().FontGlobalScale = FontScale.Get() * Scale.Get();
        FontDisplaySize.Set(ImGui.GetFontSize() * FontScale.Get() * Scale.Get());
    }

    private void Cleanup()
    {
        Controller?.Dispose();
    }

    private void Render(double delta)
    {
        Controller?.Update((float)delta);

        foreach (IElement element in Elements.ToArray())
            element.Render();

        MenuBar?.Render();
        Controller?.Render();
    }

    private List<Font> LoadFontsFromDisk()
    {
        List<Font> fonts = new();
        Dictionary<string, IPath> fontFiles = new();

        IPath[] files = FileService.GetFiles(PathService.Fonts, SearchOption.AllDirectories);

        IPath[] configFiles = files.Where(file => file.GetExtension() == ".toml").ToArray();

        foreach (IPath path in files.Where(file => file.GetExtension() == ".otf"))
            fontFiles.TryAdd(path.GetFileNameWithoutExtension(), path);

        foreach (IPath path in files.Where(file => file.GetExtension() == ".ttf"))
            fontFiles.TryAdd(path.GetFileNameWithoutExtension(), path);

        Debugger.Log($"Loading fonts from {fontFiles.Count} files and {configFiles.Length} configs...");

        foreach (IPath configFile in configFiles)
        {
            string fontName = configFile.GetFileNameWithoutExtension();

            //  Check there is an matching font file for this config
            if (!fontFiles.TryGetValue(fontName, out IPath? fontFile))
                continue;

            string content = FileService.ReadString(configFile);
            Font font = TomletMain.To<Font>(content);
            font.Name = fontName;
            font.Source = fontFile;

            if (font.IsDefault)
                fonts.Insert(0, font);
            else
                fonts.Add(font);
        }

        if (fonts.Count == 0 || !fonts[0].IsDefault)
            ImGui.GetIO().Fonts.AddFontDefault();

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

            Debugger.Log($"Loaded font '{font.Name}'. Size: {font.Size} Unicode: {font.MinUnicode}-{font.MaxUnicode} Icons: {font.IsIcons} Default: {font.IsDefault}", LogType.CONTINUED);
        }

        Debugger.Log($"...fonts loaded: {fonts.Count}", LogType.CONTINUED);
        return fonts;
    }

    public static unsafe ImFontPtr LoadFont(IPath fontFile, float fontSize, bool mergeMode, (ushort?, ushort?) charRange)
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
                0
            }, GCHandleType.Pinned);

            charRangePtr = charRangeHandle.AddrOfPinnedObject();
        }


        ImFontPtr ptr;
        try
        {
            ptr = ImGui.GetIO().Fonts.AddFontFromFileTTF(fontFile.ToString(), fontSize, config, charRangePtr);
        }
        finally
        {
            config.Destroy();

            if (charRangeHandle.IsAllocated)
                charRangeHandle.Free();
        }

        return ptr;
    }

}
