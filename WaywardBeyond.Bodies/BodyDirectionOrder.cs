using System.Collections.Generic;

namespace WaywardBeyond.Bodies;

/// <summary>
/// The canonical round-the-circle order of directional texture tags. Material selection builds an ordered
/// list by iterating this order and including only the tags a state actually defines, so index 0 is always
/// the forward-facing ("front") material and later indices step around the entity.
/// </summary>
public static class BodyDirectionOrder
{
    /// <summary>The canonical direction tags from forward-facing, stepping clockwise around the up axis.</summary>
    public static readonly string[] Preferences = ["front", "back", "left", "right"];

    /// <summary>
    /// Returns the direction textures of <paramref name="directions"/> in canonical order, only including tags
    /// present. Multiple texture paths for one direction are flattened.
    /// </summary>
    public static string[] Resolve(in Dictionary<string, string?[]> directions)
    {
        if (directions == null)
        {
            return [];
        }

        var textures = new List<string>();
        foreach (string tag in Preferences)
        {
            if (!directions.TryGetValue(tag, out string?[]? paths))
            {
                continue;
            }

            if (paths == null)
            {
                continue;
            }

            foreach (string? path in paths)
            {
                if (!string.IsNullOrEmpty(path))
                {
                    textures.Add(path);
                }
            }
        }

        return [.. textures];
    }
}