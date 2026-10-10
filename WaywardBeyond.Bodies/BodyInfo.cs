using System.Collections.Generic;

namespace WaywardBeyond.Bodies;

/// <summary>Asset information used to display a body.</summary>
public sealed class BodyInfo(in string id, in Dictionary<string, string[]> states)
{
    /// <inheritdoc cref="BodyDefinition.ID"/>
    public readonly string ID = id;
    
    /// <inheritdoc cref="BodyDefinition.States"/>
    private readonly Dictionary<string, string[]> _states = states;

    /// <summary>
    /// The ordered directional texture paths for a state tag.
    /// Empty when the body defines no such state.
    /// </summary>
    public string[] GetTextures(string tag)
    {
        return _states.TryGetValue(tag, out string[]? values) ? values : [];
    }
}