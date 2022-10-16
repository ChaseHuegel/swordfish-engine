using System.Collections.Generic;
using System.Linq;
using Swordfish.Library.Util;

namespace Swordfish.Library.Extensions
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> enumerable)
        {
            return enumerable.OrderBy((item) => MathS.Random.Next());
        }
    }
}
