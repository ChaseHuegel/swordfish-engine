using System.Collections.Generic;
using Swordfish.Library.Collections;

namespace WaywardBeyond.Bodies;

/// <summary>Provides access to all loaded <see cref="Body"/>s.</summary>
public interface IBodyDatabase : IAssetDatabase<Body>
{
    /// <summary>The ID of the default body.</summary>
    string? DefaultId { get; }

    /// <summary>The number of loaded bodies.</summary>
    int Count { get; }

    /// <summary>The IDs of every loaded body.</summary>
    IReadOnlyList<string> Ids { get; }
}