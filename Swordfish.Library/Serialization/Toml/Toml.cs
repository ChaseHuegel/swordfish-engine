using System;
using Tomlet;
using Tomlet.Models;

namespace Swordfish.Library.Serialization.Toml;

public static class Toml
{
    private static TomlSerializerOptions Options { get; } = new()
    {
        OverrideConstructorValues = true,
        IgnoreNonPublicMembers = false,
        IgnoreInvalidEnumValues = true
    };

    public static TomlValue ValueFrom(Type type, object t)
    {
        return TomletMain.ValueFrom(type, t, Options);
    }

    public static T To<T>(TomlValue value)
    {
        return TomletMain.To<T>(value, Options);
    }

    public static T To<T>(string tomlString)
    {
        return TomletMain.To<T>(tomlString, Options);
    }
    
    public static string From<T>(T t)
    {
        if (t == null)
        {
            return null!;
        }

        return TomletMain.TomlStringFrom(t.GetType(), t, Options);
    }
}