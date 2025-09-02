using System.Globalization;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Reef;
using Reef.MSDF;
using Reef.Text;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.IO;
using Tomlet;

namespace Swordfish.UI.Reef;

public sealed class ReefContext : IDisposable
{
    public readonly UIBuilder<Material> Builder;
    public readonly ITextEngine TextEngine;

    private readonly ILogger _logger;
    private readonly IWindow _window;
    private readonly IInputService _input;
    private readonly VirtualFileSystem _vfs;
    private readonly UIController _controller;

    public ReefContext(ILogger logger, IWindow window, IInputService input, VirtualFileSystem vfs)
    {
        _logger = logger;
        _window = window;
        _input = input;
        _vfs = vfs;
        _controller = new UIController();

        TextEngine = CreateTextEngine();        
        Builder = new UIBuilder<Material>(width: window.Size.X, height: window.Size.Y, TextEngine, _controller);

        window.Resize += OnWindowResize;
        window.Update += OnWindowUpdate;
    }
    
    public void Dispose()
    {
        _window.Resize -= OnWindowResize;
        _window.Update -= OnWindowUpdate;
    }
    
    private void OnWindowResize(Vector2D<int> size)
    {
        Builder.Resize(size.X, size.Y);
    }

    private void OnWindowUpdate(double delta)
    {
        Vector2 cursorPos = _input.CursorPosition;
        
        var mouseButtons = UIController.MouseButtons.None;
        
        if (_input.IsMouseHeld(MouseButton.Left))
        {
            mouseButtons |= UIController.MouseButtons.Left;
        }
        
        if (_input.IsMouseHeld(MouseButton.Right))
        {
            mouseButtons |= UIController.MouseButtons.Right;
        }
        
        if (_input.IsMouseHeld(MouseButton.Middle))
        {
            mouseButtons |= UIController.MouseButtons.Middle;
        }
        
        _controller.UpdateMouse((int)cursorPos.X, (int)cursorPos.Y, mouseButtons);
    }

    private TextEngine CreateTextEngine()
    {
        List<FontInfo> fontInfos = [];

        PathInfo[] fontPaths = _vfs.GetFiles(AssetPaths.Fonts, SearchOption.AllDirectories).Where(PathIsFont).ToArray();
        PathInfo[] configPaths = _vfs.GetFiles(AssetPaths.Fonts, SearchOption.AllDirectories).Where(PathIsToml).ToArray();

        foreach (PathInfo path in fontPaths)
        {
            FontInfo fontInfo;
            string name = path.GetFileNameWithoutExtension();
            PathInfo configPath = configPaths.FirstOrDefault(
                pathInfo => pathInfo.GetFileNameWithoutExtension().Equals(name, StringComparison.InvariantCultureIgnoreCase)
            );

            //  If there isn't a config for this font, use default values.
            if (configPath.OriginalString == null)
            {
                fontInfo = new FontInfo(id: name, path: path);
                fontInfos.Add(fontInfo);
                continue;
            }

            string configContent = configPath.ReadString();
            var config = TomletMain.To<Font>(configContent);

            int minUnicode = int.Parse(config.MinUnicode, NumberStyles.HexNumber);
            int maxUnicode = int.Parse(config.MaxUnicode, NumberStyles.HexNumber);
            fontInfo = new FontInfo(id: name, path: path, minUnicode: minUnicode, maxUnicode: maxUnicode);

            if (config.IsDefault)
            {
                fontInfos.Insert(0, fontInfo);
            }
            else
            {
                fontInfos.Add(fontInfo);
            }
        }

        foreach (FontInfo fontInfo in fontInfos)
        {
            _logger.LogInformation("Loading font '{Name}'. Unicode: {MinUnicode}-{MaxUnicode}", fontInfo.ID, fontInfo.MinUnicode, fontInfo.MaxUnicode);
        }

        _logger.LogInformation("Creating Reef text engine..."); 
        var textEngine = new TextEngine(fonts: fontInfos.ToArray());
        _logger.LogInformation("Created Reef text engine.");
        return textEngine;
        
        bool PathIsFont(PathInfo path)
        {
            return path.GetExtension().Equals(".otf", StringComparison.InvariantCultureIgnoreCase) 
                   || path.GetExtension().Equals(".ttf", StringComparison.InvariantCultureIgnoreCase);
        }
        
        bool PathIsToml(PathInfo path)
        {
            return path.GetExtension().Equals(".toml", StringComparison.InvariantCultureIgnoreCase);
        }
    }
}