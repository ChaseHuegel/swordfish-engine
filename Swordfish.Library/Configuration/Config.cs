using System.IO;
using System.Text;
using Swordfish.Library.IO;
using Tomlet.Attributes;

namespace Swordfish.Library.Configuration;

public abstract class Config<T> : Toml<T>
{
    [TomlNonSerialized]
    public PathInfo Path { get; set; }

    [TomlNonSerialized]
    private readonly object _saveLock = new();

    public void Save()
    {
        lock (_saveLock)
        {
            PathInfo directory = Path.GetDirectory();
            if (!string.IsNullOrEmpty(directory.Value))
            {
                directory.CreateDirectory();
            }

            string str = ToString();
            byte[] buffer = Encoding.UTF8.GetBytes(str);

            using var stream = new MemoryStream(buffer);
            Path.Write(stream);
        }
    }
}