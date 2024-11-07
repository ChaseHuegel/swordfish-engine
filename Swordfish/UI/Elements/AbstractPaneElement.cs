using ImGuiNET;
using Swordfish.Types;

namespace Swordfish.UI.Elements;

public abstract class AbstractPaneElement(in string? name) : ContentElement, IUniqueNameProperty, IConstraintsProperty,
    IFlagsProperty<ImGuiWindowFlags>, ITooltipProperty
{
    public string? Name { get; set; } = name;

    public RectConstraints Constraints { get; set; } = new();

    public ImGuiWindowFlags Flags { get; set; }

    public Tooltip Tooltip { get; set; }

    protected ITooltipProperty TooltipProperty => this;

    public string UniqueName => _uniqueName ??= Name + "##" + UID;
    private string? _uniqueName;
}
