using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Reef.Generator;

internal sealed class StyleLayer
{
    public string? ConditionRaw;
    public RssPseudoState State;
    public readonly List<MarkupAssignment> Assignments = [];
    public Location Location = Location.None;

    public StyleLayer(RssPseudoState state, Location location, string? conditionRaw = null)
    {
        State = state;
        Location = location;
        ConditionRaw = conditionRaw;
    }
}

internal sealed class StylePlan
{
    public readonly List<StyleLayer> Normal = [];
    public readonly List<StyleLayer> Inline = [];
    public readonly List<StyleLayer> Final = [];
}

internal sealed class CallerPlan
{
    public readonly List<StyleLayer> Final = [];
    public string? RootId;
    public readonly List<RmlAttribute> ExtraCallbacks = [];
    public bool Applied;
}

internal sealed class SlotInfo
{
    public string Name = "default";
    public string ParameterName = "content_default";
    public bool HasDefault;
    public Location Location = Location.None;
}

internal sealed class SlotContent
{
    public readonly List<RmlNode> Nodes = [];
    public ExpressionScope Scope = null!;
    public string Builder = "ui";
    public string IdPrefix = string.Empty;
}

internal sealed class PseudoVariables
{
    public string? Disabled;
    public string? Hover;
    public string? Focus;
    public string? Active;
}
