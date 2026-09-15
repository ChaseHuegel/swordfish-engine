using System.Collections.Generic;
using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Core.UI.Layers;

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

        for (var i = 0; i < _nameplates.Count; i++)
        {
            NameplateInfo nameplate = _nameplates[i];
            using (ui.Text(nameplate.Name, nameplate.FontSize))
            {
                ui.ID = $"nameplate_{nameplate.Entity}";
                ui.BackgroundColor = new Vector4(0f, 0f, 0f, 0.5f);
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Local | Anchors.Bottom | Anchors.Center,
                    X = new Fixed(nameplate.X),
                    Y = new Fixed(nameplate.Y),
                };
            }
        }

        return Result.FromSuccess();
    }
}