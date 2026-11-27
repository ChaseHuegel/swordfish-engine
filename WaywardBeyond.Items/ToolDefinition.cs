namespace WaywardBeyond.Items;

/// <summary>Defines tool behavior attached to an item.</summary>
public struct ToolDefinition()
{
    /// <summary>The tool behavior kind.</summary>
    public ToolType Type;

    /// <summary>The optional target pattern or mode string for the tool.</summary>
    public string Target = string.Empty;
}
