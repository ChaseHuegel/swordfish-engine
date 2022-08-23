using Swordfish.Library.Diagnostics;
using Swordfish.Plugins;
using Swordfish.Rendering;

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
    }
}