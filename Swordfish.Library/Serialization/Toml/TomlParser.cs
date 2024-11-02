using Swordfish.Library.IO;
using Tomlet;

namespace Swordfish.Library.Serialization.Toml;

public class TomlParser<T> : IFileParser<T>
{
    public string[] SupportedExtensions { get; } = [".toml"];

    object IFileParser.Parse(IFileService fileService, PathInfo file) => Parse(fileService, file)!;
    public T Parse(IFileService fileService, PathInfo file)
    {
        return TomletMain.To<T>(fileService.ReadString(file));
    }
}