using System;
using System.Collections.Generic;
using Swordfish.ECS;

namespace WaywardBeyond.Client.Core.UI;

/// <summary>
/// One projected screen-space nameplate produced by the ECS <see cref="Systems.NameplateSystem"/> each
/// tick and consumed by <see cref="Layers.NameplateUILayer"/> on the window thread.
/// </summary>
/// <param name="Entity">The remote player entity the nameplate belongs to.</param>
/// <param name="Name">The player display name.</param>
/// <param name="X">Screen-space pixel X where the nameplate is centered.</param>
/// <param name="Y">Screen-space pixel Y where the nameplate bottom sits (the head position).</param>
/// <param name="FontSize">Font size scaled by camera distance.</param>
public readonly record struct NameplateInfo(Uuid Entity, string Name, int X, int Y, int FontSize);

/// <summary>
/// Cross-thread handoff for nameplates. The ECS nameplate system writes projected positions every
/// tick (ECS thread) and the nameplate UI layer reads them while building Reef UI on the window thread.
/// </summary>
public sealed class NameplateSnapshot
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