using Tomlet;

namespace Swordfish.Library.Configuration;

public abstract class Toml<T>
{
    public override string ToString()
    {
        return TomletMain.TomlStringFrom(this);
    }

    // ReSharper disable once UnusedMember.Global
    public static T FromString(string value)
    {
        return TomletMain.To<T>(value);
    }
}