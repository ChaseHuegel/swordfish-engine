using Swordfish.Library.IO;
using Tomlet;

namespace Swordfish.Library.Serialization.Toml;

public class TomlParser<T> : IFileParser<T>
{
    public string[] SupportedExtensions { get; } = [".toml"];

    object IFileParser.Parse(IFileService fileService, IPath file) => Parse(fileService, file)!;
    public T Parse(IFileService fileService, IPath file)
    {
        return TomletMain.To<T>(fileService.ReadString(file));
    }
}