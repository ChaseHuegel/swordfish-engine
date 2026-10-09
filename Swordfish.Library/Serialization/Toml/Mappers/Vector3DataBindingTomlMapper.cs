#nullable enable
using System;
using System.Numerics;
using Swordfish.Library.Types;
using Tomlet.Models;

namespace Swordfish.Library.Serialization.Toml.Mappers;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class Vector3DataBindingTomlMapper : TomlMapper<DataBinding<Vector3>>
{
    protected override TomlValue? Serialize(DataBinding<Vector3>? value)
    {
        Vector3 gravity = value?.Get() ?? default;
        float[] valueArray = [gravity.X, gravity.Y, gravity.Z];
        return Toml.ValueFrom(typeof(float[]), valueArray);
    }

    protected override DataBinding<Vector3> Deserialize(TomlValue value)
    {
        float[] components = Toml.To<float[]>(value);
        if (components.Length != 3)
        {
            throw new InvalidOperationException("Vector3 config values must be an array of three numbers.");
        }

        var vector = new Vector3(
            components[0],
            components[1],
            components[2]
        );
        
        return new DataBinding<Vector3>(vector);
    }
}