using System.Collections.Generic;

namespace WaywardBeyond.Client.Core.UI;

/// <summary>
/// Cross-thread handoff for nameplates: the ECS nameplate system writes projected positions each tick and
/// the nameplate UI layer reads them while building Reef UI on the window thread.
/// </summary>
internal sealed class NameplateSnapshot
{
    private readonly object _lock = new();
    private readonly List<NameplateInfo> _nameplates = [];

    public void Update(List<NameplateInfo> nameplates)
    {
        lock (_lock)
        {
            _nameplates.Clear();
            _nameplates.AddRange(nameplates);
        }
    }

    public void CopyTo(List<NameplateInfo> destination)
    {
        lock (_lock)
        {
            destination.Clear();
            destination.AddRange(_nameplates);
        }
    }
}