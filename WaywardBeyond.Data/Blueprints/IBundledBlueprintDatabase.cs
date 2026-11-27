using System.Collections.Generic;
using Swordfish.Library.Collections;

namespace WaywardBeyond.Data.Blueprints;

/// <summary>Provides headless read-only access to bundled <c>.wbp</c> blueprint assets mounted in the virtual file system.</summary>
public interface IBundledBlueprintDatabase : IAssetDatabase<BlueprintData>
{
    /// <summary>Returns metadata summaries for all loaded bundled blueprints ordered by display name and identifier.</summary>
    IReadOnlyList<BlueprintSummary> ListSummaries();
}
