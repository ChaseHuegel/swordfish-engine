using System.Collections.Generic;

namespace Swordfish.Library.Serialization;

public class DirectParser : IParser
{
    public List<byte[]> Parse(byte[] data)
    {
        return [data];
    }
}