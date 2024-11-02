using Tomlet;

namespace Swordfish.Library.Configuration;

public abstract class TomlConfiguration<T>
{
    public override string ToString()
    {
        return TomletMain.TomlStringFrom(this);
    }

    public static T FromString(string value)
    {
        return TomletMain.To<T>(value);
    }
}