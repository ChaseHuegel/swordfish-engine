using System.Collections.Generic;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>Display data for the nameplate buffer.</summary>
internal readonly record struct NameplateState(IReadOnlyList<NameplateInfo> Nameplates);

/// <summary>Draws one text element per remote player nameplate.</summary>
internal partial class NameplateView
{
}
