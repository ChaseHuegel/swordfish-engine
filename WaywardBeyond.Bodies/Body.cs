using System;
using System.Collections.Generic;

namespace WaywardBeyond.Bodies;

/// <summary>An asset for displaying a character's body.</summary>
public sealed class Body(in string id, in string species, in Gender gender, in Dictionary<string, string[]> states)
{
    /// <inheritdoc cref="BodyDefinition.ID"/>
    public readonly string ID = id;

    /// <inheritdoc cref="BodyDefinition.Species"/>
    public readonly string Species = species;

    /// <inheritdoc cref="BodyDefinition.Gender"/>
    public readonly Gender Gender = gender;

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
