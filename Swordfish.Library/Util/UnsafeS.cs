using System.Text;
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Util;

public static unsafe class UnsafeS
{
    public static string ToString(byte* ptr)
    {
        var stringBuilder = new StringBuilder();

        var offset = 0;
        while (ptr[offset] != '\0')
        {
            stringBuilder.Append((char)ptr[offset]);
            offset++;
        }

        return stringBuilder.ToString();
    }

    public static string ToString(char* ptr)
    {
        var stringBuilder = new StringBuilder();

        var offset = 0;
        while (ptr[offset] != '\0')
        {
            stringBuilder.Append(ptr[offset]);
            offset++;
        }

        return stringBuilder.ToString();
    }
}