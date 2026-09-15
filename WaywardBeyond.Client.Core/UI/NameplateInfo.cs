using Swordfish.ECS;

namespace WaywardBeyond.Client.Core.UI;

/// <summary>
/// A projected screen-space nameplate, produced by the ECS <see cref="Systems.NameplateSystem"/> each tick
/// and consumed by <see cref="Layers.NameplateUILayer"/> on the window thread.
/// </summary>
internal readonly struct NameplateInfo(Uuid entity, string name, int x, int y, int fontSize)
{
    public readonly Uuid Entity = entity;
    public readonly string Name = name;
    public readonly int X = x;
    public readonly int Y = y;
    public readonly int FontSize = fontSize;
}