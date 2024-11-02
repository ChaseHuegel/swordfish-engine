using System.Collections.Generic;

namespace Swordfish.Library.Serialization;

public interface IParser
{
    List<byte[]> Parse(byte[] data);
}
