using System.Collections.Generic;
using WaywardBeyond.Client.UI.Components;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>Display data for the hotbar.</summary>
internal readonly record struct HotbarState(IReadOnlyList<ItemSlotState> Slots);

/// <summary>Draws the nine-slot hotbar.</summary>
internal partial class HotbarView
{
}
