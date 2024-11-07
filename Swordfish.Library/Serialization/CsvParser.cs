using System.Collections.Generic;

namespace Swordfish.Library.Serialization;

// ReSharper disable once UnusedType.Global
public class CsvParser : IParser
{
    public List<byte[]> Parse(byte[] data)
    {
        var dataPackets = new List<byte[]>();

        var offset = 0;
        for (var i = 0; i < data.Length; i++)
        {
            if (data[i] == ',')
            {
                dataPackets.Add(data[offset..i]);
                i++;
                offset = i;
            }
            else if (i == data.Length - 1)
            {
                dataPackets.Add(data[offset..]);
            }
        }

        return dataPackets;
    }
}