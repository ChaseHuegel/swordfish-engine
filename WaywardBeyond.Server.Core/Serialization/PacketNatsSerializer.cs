using System;
using System.Buffers;
using NATS.Client.Core;
using Torches.Networking.Models;

namespace WaywardBeyond.Server.Core.Serialization;

internal sealed class PacketNatsSerializer : INatsSerializer<Packet>
{
    public void Serialize(IBufferWriter<byte> bufferWriter, Packet value)
    {
        Span<byte> span = bufferWriter.GetSpan();
        value.SerializeInto(span);
    }

    public Packet Deserialize(in ReadOnlySequence<byte> buffer)
    {
        if (buffer.IsSingleSegment)
        {
            return Packet.Deserialize(buffer.FirstSpan);
        }

        //  TODO When needlefish supports ReadOnlySequence update this to not allocate
        byte[] array = buffer.ToArray();
        return Packet.Deserialize(array, start: 0, array.Length);
    }

    public INatsSerializer<Packet> CombineWith(INatsSerializer<Packet> next)
    {
        return next;
    }
}