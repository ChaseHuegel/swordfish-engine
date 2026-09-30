using System.Collections.Generic;

namespace WaywardBeyond.Shared.Bodies;

/// <summary>
/// The headless, render-free view of a loaded body model. Carries the stable string ID and the ordered
/// directional texture paths for each state. It does not reference a graphics material or mesh, so the
/// server and headless consumers share it; the client resolves texture paths to renderable materials.
/// </summary>
public sealed class BodyInfo
{
    public readonly string ID;
    public readonly Dictionary<string, string[]> States;

    public BodyInfo(in string id, in Dictionary<string, string[]> states)
    {
        ID = id;
        States = states;
    }

    /// <summary>
    /// The ordered directional texture paths for a state tag, in canonical
    /// <see cref="BodyDirectionOrder"/>. Empty when the body defines no such state.
    /// </summary>
    public string[] GetState(string stateTag)
    {
        return States.TryGetValue(stateTag, out string[]? values) ? values : [];
    }
}