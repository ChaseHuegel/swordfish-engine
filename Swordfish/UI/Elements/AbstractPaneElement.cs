using ImGuiNET;
using Swordfish.Types;

namespace Swordfish.UI.Elements;

public abstract class AbstractPaneElement : ContentElement, IUniqueNameProperty, IConstraintsProperty, IFlagsProperty<ImGuiWindowFlags>, ITooltipProperty
{
    public string? Name { get; set; }

    public RectConstraints Constraints { get; set; } = new RectConstraints();

    public ImGuiWindowFlags Flags { get; set; }

    public Tooltip Tooltip { get; set; }

    protected ITooltipProperty TooltipProperty => this;

    public string UniqueName => uniqueName ??= Name + "##" + Uid;
    private string? uniqueName;

    public AbstractPaneElement(string? name)
    {
        Name = name;
    }
}
