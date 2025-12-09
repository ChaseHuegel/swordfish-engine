using System.Diagnostics.CodeAnalysis;
using Swordfish.Library.Configuration;
using Swordfish.Library.Types;

namespace WaywardBeyond.Client.Core.Configuration;

[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Local")]
public sealed class VolumeSettings : Config<VolumeSettings>
{
    public DataBinding<float> Master { get; private set; } = new(0.5f);
    public DataBinding<float> Interface { get; private set; } = new(0.5f);
    public DataBinding<float> Effects { get; private set; } = new(0.5f);
    public DataBinding<float> Music { get; private set; } = new(0.5f);

    public float MixInterface() => Master * Interface;
    public float MixEffects() => Master * Effects;
    public float MixMusic() => Master * Music;
}