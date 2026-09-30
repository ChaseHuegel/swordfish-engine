using System.Collections.Generic;

namespace WaywardBeyond.Shared.Bodies;

/// <summary>
/// The directional texture set for a single body state. <see cref="Directions"/> maps a direction tag
/// ("front", "back", "left", "right", ...) to the texture paths rendered for that view. Order is derived
/// from <see cref="BodyDirectionOrder"/> rather than the (unordered) dictionary, so "front" is always the
/// forward-facing index 0 a billboard centers its first sector on.
/// </summary>
public struct BodyStateTextures()
{
    public Dictionary<string, string?[]> Directions;
}