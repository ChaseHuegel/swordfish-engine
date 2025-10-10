#nullable enable
using Swordfish.Library.Types;
using Tomlet;
using Tomlet.Models;

namespace Swordfish.Library.Serialization.Toml.Mappers;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class DataBindingTomlMapper<T> : TomlMapper<DataBinding<T>>
{
    protected override TomlValue Serialize(DataBinding<T>? value)
    {
        T? backingValue = value != null ? value.Get() : default;
        return TomletMain.ValueFrom(typeof(T), backingValue!)!;
    }

    protected override DataBinding<T> Deserialize(TomlValue value)
    {
        var backingValue = TomletMain.To<T>(value);
        return new DataBinding<T>(backingValue);
    }
}