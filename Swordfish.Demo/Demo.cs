using Swordfish.Demo.UI;
using Swordfish.Library.Diagnostics;
using Swordfish.Plugins;

namespace Swordfish.Demo;

public class Demo : IPlugin
{
    public void Load()
    {
        Debug.Log("Demo loaded!");
    }

    public void Unload()
    {
        Debug.Log("Demo unloaded!");
    }

    public void Initialize()
    {
        Debug.Log("Demo initialized!");

        TestUI.CreateCanvas();
    }
}