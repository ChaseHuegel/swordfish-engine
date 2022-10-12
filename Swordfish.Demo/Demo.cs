using Ninject;
using Swordfish.Demo.ECS;
using Swordfish.Demo.UI;
using Swordfish.ECS;
using Swordfish.Extensibility;
using Swordfish.Library.Diagnostics;
using Debugger = Swordfish.Library.Diagnostics.Debugger;

namespace Swordfish.Demo;

public class Demo : Mod
{
    public override string Name => "Swordfish Demo";
    public override string Description => "A demo of the Swordfish engine.";

    public override void Load()
    {
        IECSContext ecsContext = SwordfishEngine.Kernel.Get<IECSContext>();
        DemoComponent.Index = ecsContext.BindComponent<DemoComponent>();
    }

    public override void Initialize()
    {
        TestUI.CreateCanvas();
        TestECS.Populate();
        Benchmark.Log();
    }

    public override void Unload()
    {
        Debugger.Log("Dumping the log.");
        Debugger.Dump();
    }
}