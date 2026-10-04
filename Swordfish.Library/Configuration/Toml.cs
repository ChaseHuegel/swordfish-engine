using Swordfish.Library.Serialization.Toml;

namespace Swordfish.Library.Configuration;

public abstract class Toml<T>
{
    public override string ToString()
    {
        return Toml.From(this);
    }

    // ReSharper disable once UnusedMember.Global
    public static T FromString(string value)
    {
        return Toml.To<T>(value);
    }
}