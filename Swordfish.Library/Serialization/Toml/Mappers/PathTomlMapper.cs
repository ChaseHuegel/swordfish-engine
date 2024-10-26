#nullable enable
using Swordfish.Library.IO;
using Tomlet.Exceptions;
using Tomlet.Models;
using Path = Swordfish.Library.IO.Path;

namespace Swordfish.Library.Serialization.Toml.Mappers;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class PathInterfaceTomlMapper : TomlMapper<IPath>
{
    protected override TomlValue? Serialize(IPath? path)
    {
        return path is null ? null : new TomlString(path.ToString());
    }

    protected override IPath Deserialize(TomlValue value)
    {
        if (value is not TomlString tomlString)
        {
            throw new TomlTypeMismatchException(typeof(TomlString), value.GetType(), typeof(Path));
        }

        return new Path(tomlString.Value);
    }
}

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class PathTomlMapper : TomlMapper<Path>
{
    protected override TomlValue Serialize(Path path)
    {
        return new TomlString(path.ToString());
    }

    protected override Path Deserialize(TomlValue value)
    {
        if (value is not TomlString tomlString)
        {
            throw new TomlTypeMismatchException(typeof(TomlString), value.GetType(), typeof(Path));
        }

        return new Path(tomlString.Value);
    }
}