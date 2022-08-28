using ImGuiNET;
using Swordfish.Extensibility;
using Swordfish.Library.Types.Constraints;
using Swordfish.Types.Constraints;
using Swordfish.UI.Elements;

namespace Swordfish.Editor;

public class Editor : Plugin
{
    public override string Name => "Swordfish Editor";
    public override string Description => "Visual editor for the Swordfish engine.";

    public override void Load()
    {
        SwordfishEngine.MainWindow.Maximize();
    }

    public override void Unload()
    {
    }

    public override void Initialize()
    {
        CanvasElement heirarchy = new("Hierarchy")
        {
            Flags = ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoMove,
            Constraints = new RectConstraints
            {
                Width = new RelativeConstraint(0.15f),
                Height = new RelativeConstraint(1f)
            }
        };
    }
}