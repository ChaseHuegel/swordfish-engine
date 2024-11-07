using System;
using System.Collections.Generic;

namespace Swordfish.Library.Serialization;

// ReSharper disable once UnusedType.Global
public class LengthDelimitedParser : IParser
{
    public List<byte[]> Parse(byte[] data)
    {
        var dataPackets = new List<byte[]>();

        int packetLength;
        for (var i = 0; i < data.Length; i += packetLength + 4)
        {
            packetLength = BitConverter.ToInt32(data, i);
            dataPackets.Add(data[(i + 4)..(i + 4 + packetLength)]);
        }

        return dataPackets;
    }
}