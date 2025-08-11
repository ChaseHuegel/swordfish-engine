using System.Collections.Generic;
using Swordfish.Bricks;

namespace WaywardBeyond.Client.Core.Bricks;

internal static class BrickRegistry
{
    public static readonly Brick ShipCore = new(id: BrickGridBuilder.BLOCK) { Name = "ship_core" };
    public static readonly Brick Rock = new(id: BrickGridBuilder.BLOCK) { Name = "rock" };
    public static readonly Brick Ice = new(id: BrickGridBuilder.BLOCK) { Name = "ice" };
    public static readonly Brick MetalPanel = new(id: BrickGridBuilder.BLOCK) { Name = "metal_panel" };
    public static readonly Brick CautionPanel = new(id: BrickGridBuilder.BLOCK) { Name = "caution_panel" };
    public static readonly Brick DisplayControl = new(id: BrickGridBuilder.BLOCK) { Name = "display_control" };
    public static readonly Brick Thruster = new(id: BrickGridBuilder.THRUSTER) { Name = "thruster" };

    public static readonly IReadOnlyDictionary<string, Brick> Bricks = new Dictionary<string, Brick>
    {
        { ShipCore.Name!, ShipCore },
        { Rock.Name!, Rock },
        { Ice.Name!, Ice },
        { MetalPanel.Name!, MetalPanel },
        { CautionPanel.Name!, CautionPanel },
        { DisplayControl.Name!, DisplayControl },
        { Thruster.Name!, Thruster },
    };
}