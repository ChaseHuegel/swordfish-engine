using System.Collections.Generic;
using System.Linq;
using Swordfish.Library.Util;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Extensions;

// ReSharper disable once UnusedType.Global
public static partial class EnumerableExtensions
{
    public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> enumerable)
    {
        return enumerable.OrderBy(_ => MathS.Random.Next());
    }
}