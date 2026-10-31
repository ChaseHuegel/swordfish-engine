using System.Collections.Generic;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>
/// Draws the screen-space nameplates written by the ECS <see cref="Systems.NameplateSystem"/> as Reef text,
/// one text element per remote player, bottom-centered over the projected head position and font-sized by
/// distance. Pure presentation: all projection happens on the ECS thread; this layer only renders the buffer.
/// </summary>
internal sealed class NameplateUILayer(in NameplateSnapshot snapshot) : IUILayer
{
    private readonly NameplateSnapshot _snapshot = snapshot;
    private readonly List<NameplateInfo> _nameplates = [];

    public bool IsVisible()
    {
        return WaywardBeyond.GameState == GameState.Playing;
    }

    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        _snapshot.CopyTo(_nameplates);
        new NameplateView().Render(ui, new NameplateState(_nameplates));

        return Result.FromSuccess();
    }
}
