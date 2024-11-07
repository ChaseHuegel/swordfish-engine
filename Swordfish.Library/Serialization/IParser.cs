using System.Collections.Generic;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Serialization;

public interface IParser
{
    List<byte[]> Parse(byte[] data);
}
