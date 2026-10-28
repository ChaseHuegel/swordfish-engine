using Reef;
using Reef.UI;

namespace Reef.Markup.TestComponents;

/// <summary>A component with a required constructor argument.</summary>
public partial class CaptionedCard
{
    private readonly string _prefix;

    public CaptionedCard(string prefix)
    {
        _prefix = prefix;
    }
}

/// <summary>Context used by the CaptionedCard component.</summary>
public sealed class CaptionState
{
    public string Prefix { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
