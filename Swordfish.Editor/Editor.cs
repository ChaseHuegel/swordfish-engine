using ImGuiNET;
using Swordfish.Extensibility;
using Swordfish.Library.Types.Constraints;
using Swordfish.Types.Constraints;
using Swordfish.UI.Elements;

namespace Swordfish.Editor;

public class Editor : Plugin
{
    private const ImGuiWindowFlags EDITOR_CANVAS_FLAGS = ImGuiWindowFlags.AlwaysAutoResize
        | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoCollapse;

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
        MenuElement menu = new();

        CanvasElement heirarchy = new("Hierarchy")
        {
            Flags = EDITOR_CANVAS_FLAGS,
            Constraints = new RectConstraints
            {
                Width = new RelativeConstraint(0.15f),
                Height = new RelativeConstraint(1f)
            }
        };

        CanvasElement console = new("Console")
        {
            Flags = EDITOR_CANVAS_FLAGS,
            Constraints = new RectConstraints
            {
                X = new RelativeConstraint(0.15f),
                Y = new RelativeConstraint(0.8f),
                Width = new RelativeConstraint(0.4f),
                Height = new RelativeConstraint(0.2f)
            }
        };

        CanvasElement assetBrowser = new("Asset Browser")
        {
            Flags = EDITOR_CANVAS_FLAGS,
            Constraints = new RectConstraints
            {
                X = new RelativeConstraint(0.55f),
                Y = new RelativeConstraint(0.8f),
                Width = new RelativeConstraint(0.3f),
                Height = new RelativeConstraint(0.2f)
            }
        };

        CanvasElement inspector = new("Inspector")
        {
            Flags = EDITOR_CANVAS_FLAGS,
            Constraints = new RectConstraints
            {
                Anchor = ConstraintAnchor.TOP_RIGHT,
                Width = new RelativeConstraint(0.15f),
                Height = new RelativeConstraint(1f)
            }
        };
    }
}