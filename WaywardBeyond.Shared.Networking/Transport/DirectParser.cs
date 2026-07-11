using System.Collections.Generic;

namespace WaywardBeyond.Shared.Networking.Transport;

public class DirectParser : IParser
{
    public List<byte[]> Parse(byte[] data)
    {
        return [data];
    }
}
