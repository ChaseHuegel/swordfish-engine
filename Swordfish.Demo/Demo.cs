using Swordfish.Library.Diagnostics;
using Swordfish.Plugins;
using Swordfish.Rendering;
using Swordfish.UI.Elements;

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

        Canvas myCanvas = new("My Canvas")
        {
            Content = {
                new TextElement("My text."),
                new TextElement("Disabled text.") {
                    Enabled = false
                },
                new TextElement("Non visible text.") {
                    Visible = false
                },
                new TextElement("There is non-visible text above this."),
            }
        };
    }
}