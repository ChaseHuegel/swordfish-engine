using System.Collections.Generic;

namespace WaywardBeyond.Bodies;

/// <summary>Defines a visual body asset.</summary>
internal struct BodyDefinition()
{
    /// <summary>The unique ID of the body.</summary>
    public string? ID;

    /// <summary>
    ///     Maps schemaless states by tag to schemaless textures by tag.
    ///     All tags are case-insensitive.
    ///     <para/>
    ///     Outer keys are state tags ("standing", "floating").
    ///     Inner keys are texture tags ("front", "back").
    ///     Inner values are texture paths ("characters/player_front.png").
    /// </summary>
    public Dictionary<string, Dictionary<string, string?[]>>? States;
}