using System;
using System.IO;
using System.Numerics;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Serialization.Toml.Mappers;
using Swordfish.Library.Types;
using Tomlet;
using Tomlet.Models;
using Xunit;

namespace Swordfish.Tests;

public class Vector3DataBindingTomlMapperTests
{
    [Fact]
    public void Vector3DataBindingRoundTripsTomlArray()
    {
        new Vector3DataBindingTomlMapper().Register();

        var gravity = new DataBinding<Vector3>(new Vector3(1f, -2.5f, 3f));
        TomlValue serialized = Toml.ValueFrom(typeof(DataBinding<Vector3>), gravity);

        DataBinding<Vector3> parsed = Toml.To<DataBinding<Vector3>>(serialized);
        Assert.Equal(gravity.Get(), parsed.Get());
    }

    [Fact]
    public void Vector3DataBindingParsesFromTomlDocument()
    {
        new Vector3DataBindingTomlMapper().Register();

        string path = Path.Combine(Path.GetTempPath(), $"gravity-{Guid.NewGuid():N}.toml");
        File.WriteAllText(path, "gravity = [1.0, -2.5, 3.0]");
        try
        {
            DataBinding<Vector3> parsed = Toml.To<DataBinding<Vector3>>(TomlParser.ParseFile(path).GetValue("gravity"));
            Assert.Equal(new Vector3(1f, -2.5f, 3f), parsed.Get());
        }
        finally
        {
            File.Delete(path);
        }
    }
}