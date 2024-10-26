#nullable enable
using Tomlet;
using Tomlet.Models;

namespace Swordfish.Library.Serialization.Toml;

public abstract class TomlMapper<T> : ITomlMapper
{
    public void Register()
    {
        TomletMain.RegisterMapper(Serialize, Deserialize);
    }

    protected abstract TomlValue? Serialize(T? path);

    protected abstract T Deserialize(TomlValue value);
}