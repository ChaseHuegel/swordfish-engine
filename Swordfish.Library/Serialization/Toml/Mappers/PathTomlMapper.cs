#nullable enable
using Swordfish.Library.IO;
using Tomlet.Exceptions;
using Tomlet.Models;

namespace Swordfish.Library.Serialization.Toml.Mappers;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed class PathTomlMapper : TomlMapper<PathInfo>
{
    protected override TomlValue Serialize(PathInfo value)
    {
        return new TomlString(value.Value);
    }

    protected override PathInfo Deserialize(TomlValue value)
    {
        if (value is not TomlString tomlString)
        {
            throw new TomlTypeMismatchException(typeof(TomlString), value.GetType(), typeof(PathInfo));
        }

        return new PathInfo(tomlString.Value);
    }
}