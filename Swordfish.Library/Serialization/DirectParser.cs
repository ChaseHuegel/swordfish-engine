using System.Collections.Generic;

namespace Swordfish.Library.Serialization;

// ReSharper disable once UnusedType.Global
public class DirectParser : IParser
{
    public List<byte[]> Parse(byte[] data)
    {
        return [data];
    }
}