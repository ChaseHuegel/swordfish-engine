using System.Collections.Generic;

namespace WaywardBeyond.Shared.Networking.Transport;

public interface IParser
{
    List<byte[]> Parse(byte[] data);
}
