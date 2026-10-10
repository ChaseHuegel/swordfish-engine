using System;
using System.Collections.Generic;

namespace WaywardBeyond.Bodies;

/// <summary>An asset for displaying a character's body.</summary>
public sealed class Body(in string id, in Dictionary<string, string[]> states)
{
    /// <inheritdoc cref="BodyDefinition.ID"/>
    public readonly string ID = id;
    
    /// <inheritdoc cref="BodyDefinition.States"/>
    private readonly Dictionary<string, string[]> _states = new(states, StringComparer.InvariantCultureIgnoreCase);

    /// <summary>
    ///     The ordered directional texture paths for a state tag.
    ///     All tags are case-insensitive.
    /// </summary>
    /// <returns>An array containing texture paths; empty if the tag is supported by this body.</returns>
    public string[] GetTextures(string tag)
    {
        return _states.TryGetValue(tag, out string[]? values) ? values : [];
    }
}