using BenchmarkDotNet.Attributes;
using Reef.MSDF;
using Reef.Text;

namespace Reef.Benchmarks;

[MemoryDiagnoser]
public class UIBuilderBenchmarks
{
    private readonly FontInfo _awesomeFont;
    private readonly PixelTexture _texture;
    private readonly UIBuilder<PixelTexture> _builder;
    private readonly PixelRenderer _renderer;
    private readonly List<RenderCommand<PixelTexture>> _renderCommands;
    
    public UIBuilderBenchmarks()
    {
        _texture = new PixelTexture(64, 64);
        
        var salmonFont = new FontInfo("salmon-mono-9-regular", "Fonts/Salmon Mono 9 Regular.ttf");
        _awesomeFont = new FontInfo("fa-6-free-solid", "Fonts/Font Awesome 6 Free Solid.otf");
        var fonts = new Dictionary<string, PixelFontSDF>
        {
            { salmonFont.ID, new PixelFontSDF(new PixelTexture(352, 352), 0.02f) },
            { _awesomeFont.ID, new PixelFontSDF(new PixelTexture(1272, 1272), 1f) },
        };
        
        var textEngine = new TextEngine([ salmonFont, _awesomeFont ]);
        var controller = new UIController();
        
        _builder = new UIBuilder<PixelTexture>(width: 1920, height: 1080, textEngine, controller);
        _renderer = new PixelRenderer(1920, 1080, textEngine, fonts);

        TestUI.Populate(_builder, _awesomeFont, _texture);
        _renderCommands = _builder.Build(delta: 0f);
    }

    [GlobalSetup]
    public void Setup()
    {
        TestUI.Populate(_builder, _awesomeFont, _texture);
    }

    [Benchmark]
    public List<RenderCommand<PixelTexture>> Build() => _builder.Build(delta: 0f);
    
    [Benchmark]
    public void Populate() => TestUI.Populate(_builder, _awesomeFont, _texture);
    
    [Benchmark]
    public void Render() => _renderer.Render(_renderCommands);

    [Benchmark]
    public List<RenderCommand<PixelTexture>> PopulateAndBuild()
    {
        TestUI.Populate(_builder, _awesomeFont, _texture);
        return _builder.Build(delta: 0f);
    }
    
    [Benchmark]
    public void PopulateBuildAndRender()
    {
        TestUI.Populate(_builder, _awesomeFont, _texture);
        List<RenderCommand<PixelTexture>> commands = _builder.Build(delta: 0f);
        _renderer.Render(commands);
    }
}