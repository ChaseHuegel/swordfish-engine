using System.Collections.Generic;
using Swordfish.Library.Util;

namespace WaywardBeyond.Bodies;

/// <summary>
/// Headless access to body model definitions. A body is identified by its stable string ID; consumers
/// resolve <see cref="BodyInfo"/> through <see cref="Swordfish.Library.Collections.IAssetDatabase{T}"/>.
/// </summary>
public interface IBodyDatabase
{
    /// <summary>The ID of the first loaded body, used as a default when a referenced body is unknown.</summary>
    string? DefaultId { get; }

    /// <summary>The number of loaded bodies.</summary>
    int Count { get; }

    /// <summary>The IDs of every loaded body, in insertion (parse) order.</summary>
    IEnumerable<string> Ids { get; }

    /// <summary>Whether a body with the provided ID is loaded.</summary>
    bool Contains(string id);

    /// <summary>Attempts to get a body's info by its stable string ID.</summary>
    Result<BodyInfo> Get(string id);
}