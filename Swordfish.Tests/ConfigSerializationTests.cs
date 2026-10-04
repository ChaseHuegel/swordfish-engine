using Swordfish.Library.Configuration;
using Swordfish.Library.Serialization.Toml.Mappers;
using Swordfish.Library.Types;
using Xunit;

namespace Swordfish.Tests;

public class ConfigSerializationTests
{
    private sealed class TestSettings : Config<TestSettings>
    {
        public DataBinding<int> Framerate { get; private set; } = new(60);
        public DataBinding<bool> VSync { get; private set; } = new(true);
        public DataBinding<string> Name { get; private set; } = new("default");

        [Tomlet.Attributes.TomlNonSerialized]
        public DataBinding<int> Hidden { get; private set; } = new(-1);
    }

    private static void RegisterPrimitiveMappers()
    {
        new PathTomlMapper().Register();
        new DataBindingTomlMapper<bool>().Register();
        new DataBindingTomlMapper<byte>().Register();
        new DataBindingTomlMapper<sbyte>().Register();
        new DataBindingTomlMapper<char>().Register();
        new DataBindingTomlMapper<decimal>().Register();
        new DataBindingTomlMapper<double>().Register();
        new DataBindingTomlMapper<float>().Register();
        new DataBindingTomlMapper<int>().Register();
        new DataBindingTomlMapper<uint>().Register();
        new DataBindingTomlMapper<long>().Register();
        new DataBindingTomlMapper<ulong>().Register();
        new DataBindingTomlMapper<short>().Register();
        new DataBindingTomlMapper<ushort>().Register();
        new DataBindingTomlMapper<string>().Register();
        new DataBindingTomlMapper<object>().Register();
    }

    [Fact]
    public void SerializesToNonEmptyToml()
    {
        RegisterPrimitiveMappers();

        var settings = new TestSettings();
        settings.Framerate.Set(144);
        settings.VSync.Set(false);
        settings.Name.Set("test");

        string toml = settings.ToString();

        Assert.False(string.IsNullOrWhiteSpace(toml));
        Assert.Contains("Framerate", toml);
        Assert.Contains("VSync", toml);
        Assert.Contains("Name", toml);
    }

    [Fact]
    public void RoundTripsValues()
    {
        RegisterPrimitiveMappers();

        var settings = new TestSettings();
        settings.Framerate.Set(144);
        settings.VSync.Set(false);
        settings.Name.Set("test");

        string toml = settings.ToString();
        TestSettings loaded = TestSettings.FromString(toml);

        Assert.Equal(144, loaded.Framerate.Get());
        Assert.False(loaded.VSync.Get());
        Assert.Equal("test", loaded.Name.Get());
    }
}