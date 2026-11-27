using System.Collections.Generic;
using Swordfish.Library.Util;

namespace WaywardBeyond.Data.Blueprints;

/// <summary>Provides headless read-only access to bundled <c>.wbp</c> blueprints across client and server.</summary>
public interface IBundledBlueprintDatabase
{
    /// <summary>Returns metadata summaries for all loaded bundled blueprints.</summary>
    IReadOnlyList<BlueprintSummary> ListSummaries();

    /// <summary>Gets the raw palette-encoded <see cref="BlueprintData"/> for a bundled blueprint by ID.</summary>
    Result<BlueprintData> GetBlueprint(string id);
}
