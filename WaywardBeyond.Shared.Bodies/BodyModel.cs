using System.Collections.Generic;

namespace WaywardBeyond.Shared.Bodies;

/// <summary>
/// A single body model definition. Owns a state-by-state collection of directional textures; both the
/// state keys ("standing", "floating", ...) and the direction keys ("front", "back", ...) are free-form
/// tags, so new states and directions need no schema change.
/// </summary>
public struct BodyModel()
{
    public string ID;
    public Dictionary<string, BodyStateTextures> States;
}