using BenchmarkDotNet.Attributes;

namespace Reef.Benchmarks;


[MemoryDiagnoser]
public class UIControllerBenchmarks
{
    private readonly UIController _controller;
    
    public UIControllerBenchmarks()
    {
        _controller = new UIController();
    }

    [Benchmark]
    public void UpdateMouse() => _controller.UpdateMouse(763, 536, UIController.MouseButtons.Left);
}