using Swordfish.Library.IO;
using Tomlet;

namespace Swordfish.Library.Serialization.Toml;

// ReSharper disable once ClassNeverInstantiated.Global
public class TomlParser<T> : IFileParser<T>
{
    public string[] SupportedExtensions { get; } = [".toml"];

    object IFileParser.Parse(PathInfo file) => Parse(file)!;
    public T Parse(PathInfo file)
    {
        return TomletMain.To<T>(file.ReadString());
    }
}