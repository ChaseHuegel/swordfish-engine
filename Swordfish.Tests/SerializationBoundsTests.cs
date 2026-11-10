using System;
using System.IO;
using WaywardBeyond.Data.Networking;
using Xunit;

namespace Swordfish.Tests;

/// <summary>Pins the generated serializer length ceiling and the malformed-frame guard.</summary>
public class SerializationBoundsTests
{
    [Fact]
    public void SerializeRejectsAnArrayAboveTheLengthCeiling()
    {
        var message = new LevelStreamChunk(0, 1, new byte[ushort.MaxValue + 1]);

        Assert.Throws<InvalidOperationException>(() => message.Serialize());
    }

    [Fact]
    public void SerializeRejectsAStringAboveTheLengthCeiling()
    {
        var message = new ChatMessage(0, "sender", new string('a', ushort.MaxValue + 1));

        Assert.Throws<InvalidOperationException>(() => message.Serialize());
    }

    [Fact]
    public void SerializeAcceptsTheLengthCeiling()
    {
        var message = new LevelStreamChunk(0, 1, new byte[ushort.MaxValue]);

        byte[] bytes = message.Serialize();
        LevelStreamChunk roundTrip = LevelStreamChunk.Deserialize(bytes, 0, bytes.Length);

        Assert.Equal(ushort.MaxValue, roundTrip.Payload.Length);
    }

    [Fact]
    public void DeserializeRejectsAForgedNestedLength()
    {
        //  WorldSnapshot.Components: id 2, count 1. The element claims 4 bytes. Its Payload
        //  field claims 65,535 bytes, but the element has only the 2-byte length header.
        byte[] frame =
        [
            0x02, 0x00,
            0x01, 0x00,
            0x04, 0x00,
            0x02, 0x00,
            0xFF, 0xFF,
        ];

        Assert.Throws<InvalidDataException>(() => WorldSnapshot.Deserialize(frame, 0, frame.Length));
    }

    [Fact]
    public void DeserializeRejectsATruncatedFixedArray()
    {
        //  WorldSnapshot.RemovedEntities: id 3, count 1, with no room for the 8-byte element.
        byte[] frame =
        [
            0x03, 0x00,
            0x01, 0x00,
        ];

        Assert.Throws<InvalidDataException>(() => WorldSnapshot.Deserialize(frame, 0, frame.Length));
    }
}
