using Swordfish.Demo.ECS;
using Swordfish.Demo.UI;
using Swordfish.Extensibility;
using Debugger = Swordfish.Library.Diagnostics.Debugger;

namespace Swordfish.Demo;

public class Demo : Mod
{
    public override string Name => "Swordfish Demo";
    public override string Description => "A demo of the Swordfish engine.";

    public override void Initialize()
    {
        TestUI.CreateCanvas();
        TestECS.Initialize();

        Debugger.Dump();
    }
}