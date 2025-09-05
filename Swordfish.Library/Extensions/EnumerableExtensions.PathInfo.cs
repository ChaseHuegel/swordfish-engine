using System;
using System.Collections.Generic;
using System.Linq;
using Swordfish.Library.IO;

namespace Swordfish.Library.Extensions;

public static partial class EnumerableExtensions
{
    public static IEnumerable<PathInfo> WhereToml(this IEnumerable<PathInfo> enumerable)
    {
        return enumerable.Where(IsTomlFile);

        bool IsTomlFile(PathInfo path)
        {
            return path.IsFile() && path.GetExtension().Equals(".toml", StringComparison.OrdinalIgnoreCase);
        }
    }
}