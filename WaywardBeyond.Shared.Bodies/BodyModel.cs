using System.Collections.Generic;

namespace WaywardBeyond.Shared.Bodies;

/// <summary>
/// A single body model definition. Owns a state-by-state collection of directional textures: the outer
/// keys are state tags ("standing", "floating", ...), the inner keys are direction tags ("front", "back",
/// ...). Both key sets are free-form tags, so new states and directions need no schema change.
/// </summary>
public struct BodyModel()
{
    public string ID;
    public Dictionary<string, Dictionary<string, string?[]>> States;
}