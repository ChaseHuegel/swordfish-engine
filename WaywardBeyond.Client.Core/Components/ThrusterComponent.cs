using Swordfish.ECS;

namespace WaywardBeyond.Client.Core.Components;

internal struct ThrusterComponent(in int power) : IDataComponent
{
    public int Power = power;
}