#nullable enable
using System;
using System.Numerics;
using Swordfish.Library.Types;
using Tomlet;
using Tomlet.Models;

namespace Swordfish.Library.Serialization.Toml.Mappers;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class Vector3DataBindingTomlMapper : TomlMapper<DataBinding<Vector3>>
{
    protected override TomlValue? Serialize(DataBinding<Vector3>? value)
    {
        Vector3 gravity = value != null ? value.Get() : default;

        //  Tomlet cannot map Vector3 itself: its indexer stumbles the composite serializer, so any
        //  Vector3 config payload round-trips as a three-element array of doubles.
        return Toml.ValueFrom(typeof(double[]), new[] { gravity.X, gravity.Y, gravity.Z });
    }

    protected override DataBinding<Vector3> Deserialize(TomlValue value)
    {
        double[] components = Toml.To<double[]>(value);
        if (components.Length != 3)
        {
            throw new InvalidOperationException("Vector3 config values must be an array of three numbers.");
        }

        return new DataBinding<Vector3>(new Vector3(
            (float)components[0],
            (float)components[1],
            (float)components[2]
        ));
    }
}