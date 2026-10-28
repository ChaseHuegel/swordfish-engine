using System.Collections.Generic;
using Swordfish.Library.Collections;

namespace WaywardBeyond.Bodies;

/// <summary>Provides access to all loaded <see cref="Body"/>s.</summary>
public interface IBodyDatabase : IAssetDatabase<Body>
{
    /// <summary>The ID of the default body.</summary>
    string? DefaultId { get; }

    /// <summary>The unique species IDs of every loaded body, in natural order.</summary>
    IReadOnlyList<string> Species { get; }

    /// <summary>Gets the genders available for a species, ordered by <see cref="Gender"/>.</summary>
    IReadOnlyList<Gender> GetGenders(string species);

    /// <summary>Gets the body for a species and gender, or the first variant of the species when the pair is absent.</summary>
    /// <returns>The matching body, or null when the species is unknown.</returns>
    Body? GetBody(string species, Gender gender);
}
