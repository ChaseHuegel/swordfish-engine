using System.Globalization;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Reef;
using Reef.MSDF;
using Reef.Text;
using Shoal.CommandLine;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Swordfish.Graphics;
using Swordfish.IO;
using Swordfish.Library.IO;
using Tomlet;

namespace Swordfish.UI.Reef;

public sealed class ReefContext : IDisposable
{
    private const string INPUT_REPEAT_DELAY_SEC_ARG = "INPUT_REPEAT_DELAY_SEC";
    private const string INPUT_REPEAT_INTERVAL_SEC_ARG = "INPUT_REPEAT_INTERVAL_SEC";
    private const double DEFAULT_INPUT_REPEAT_DELAY_SEC = 0.6d;
    private const double DEFAULT_INPUT_REPEAT_INTERVAL_SEC = 1d / 25;
    
    public readonly UIBuilder<Material> Builder;
    public readonly ITextEngine TextEngine;

    private readonly ILogger _logger;
    private readonly IWindow _window;
    private readonly IInputService _input;
    private readonly VirtualFileSystem _vfs;
    private readonly UIController _controller;
    private readonly double _inputRepeatDelaySec;
    private readonly double _inputRepeatIntervalSec;
    
    private readonly List<UIController.Input> _inputBuffer = [];
    private readonly Dictionary<InputEvent, double> _heldInputs = []; // InputEvent, ProcessedRepeatTime

    public ReefContext(ILogger logger, IWindow window, IInputService input, VirtualFileSystem vfs, CommandLineArgs commandLineArgs)
    {
        _logger = logger;
        _window = window;
        _input = input;
        _vfs = vfs;
        _controller = new UIController();

        if (!commandLineArgs.TryGetValue(INPUT_REPEAT_DELAY_SEC_ARG, out _inputRepeatDelaySec))
        {
            _inputRepeatDelaySec = DEFAULT_INPUT_REPEAT_DELAY_SEC;
        }
        
        if (!commandLineArgs.TryGetValue(INPUT_REPEAT_INTERVAL_SEC_ARG, out _inputRepeatIntervalSec))
        {
            _inputRepeatIntervalSec = DEFAULT_INPUT_REPEAT_INTERVAL_SEC;
        }

        TextEngine = CreateTextEngine();        
        Builder = new UIBuilder<Material>(width: window.Size.X, height: window.Size.Y, TextEngine, _controller);

        window.Resize += OnWindowResize;
        window.Update += OnWindowUpdate;
        input.KeyPressed += OnKeyPressed;
        input.KeyReleased += OnKeyReleased;
        input.CharInput += OnCharInput;
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
        
        lock (_inputBuffer)
        {
            //  Process repeating inputs
            double time = _window.Time;
            foreach ((InputEvent inputEvent, double processedRepeatTime) in _heldInputs)
            {
                double timeHeld = time - inputEvent.Time;
                if (timeHeld < _inputRepeatDelaySec)
                {
                    continue;
                }

                double repeatTime = timeHeld - _inputRepeatDelaySec - processedRepeatTime;
                if (repeatTime < _inputRepeatIntervalSec)
                {
                    continue;
                }

                var repeatCount = (int)Math.Floor(repeatTime / _inputRepeatIntervalSec);

                //  Update the amount of repeat time that's been processed
                _heldInputs[inputEvent] = processedRepeatTime + repeatTime;
                
                //  Repeat the input
                for (var i = 0; i < repeatCount; i++)
                {
                    _inputBuffer.Add(inputEvent.Input);
                }
            }
            
            //  Update the input buffer
            _controller.UpdateInputBuffer(_inputBuffer);
            _inputBuffer.Clear();
        }
    }

    private void OnKeyPressed(object? sender, KeyEventArgs e)
    {
        lock (_inputBuffer)
        {
            var key = (UIController.Key)e.Key;
            var input = new UIController.Input(key, pressed: true);
            var inputEvent = new InputEvent(input, _window.Time);
            _inputBuffer.Add(input);
            _heldInputs.TryAdd(inputEvent, 0d);
        }
    }
    
    private void OnKeyReleased(object? sender, KeyEventArgs e)
    {
        lock (_inputBuffer)
        {
            var key = (UIController.Key)e.Key;
            var input = new UIController.Input(key, pressed: false);
            var inputEvent = new InputEvent(input);
            _inputBuffer.Add(input);
            _heldInputs.Remove(inputEvent);
        }
    }
    
    private void OnCharInput(object? sender, CharEventArgs e)
    {
        lock (_inputBuffer)
        {
            var input = new UIController.Input(e.Char);
            _inputBuffer.Add(input);
        }
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
            return path.HasExtension(".otf") || path.HasExtension(".ttf");
        }
        
        bool PathIsToml(PathInfo path)
        {
            return path.HasExtension(".toml");
        }
    }
    
    private readonly struct InputEvent(in UIController.Input input, in double time = 0d) : IEquatable<InputEvent>
    {
        public readonly double Time = time;
        public readonly UIController.Input Input = input;

        public bool Equals(InputEvent other)
        {
            return Input.Key == other.Input.Key;
        }

        public override bool Equals(object? obj)
        {
            return obj is InputEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Input.Key.GetHashCode();
        }
    }
}