using Needlefish;

namespace Swordfish.Networking.Serialization;

public class PacketSerializer : ISerializer<Packet>
{
    private const int OFFSET_SEQUENCE = 0;
    private const int OFFSET_ID = 1;
    private const int OFFSET_LENGTH = 3;
    private const int OFFSET_PAYLOAD = 5;
    private const int HEADER_LENGTH = OFFSET_PAYLOAD;

    private readonly object _sequencesLock = new();
    private readonly Dictionary<ushort, byte> _sequences = new();

    public ArraySegment<byte> Serialize(object target)
    {
        //  Retrieve the packet type ID
        ushort id = (ushort)target.GetType().Name.GetHashCode();

        //  Increment sequence
        byte sequence = 0;
        lock (_sequencesLock)
        {
            if (_sequences.ContainsKey(id) && _sequences.Remove(id, out sequence))
                _sequences.Add(id, ++sequence);
            else
                _sequences.Add(id, 0);
        }

        //  Prepare buffers for writing
        byte[] payload = NeedlefishFormatter.Serialize(target);
        byte[] buffer = new byte[HEADER_LENGTH + payload.Length];

        //  Write sequence
        buffer[OFFSET_SEQUENCE] = sequence;

        //  Write ID
        Span<byte> idSpan = new Span<byte>(buffer, OFFSET_ID, 2);
        BitConverter.TryWriteBytes(idSpan, id);

        //  Write payload length
        Span<byte> lengthSpan = new Span<byte>(buffer, OFFSET_LENGTH, 2);
        BitConverter.TryWriteBytes(lengthSpan, (ushort)payload.Length);

        //  Write payload
        payload.CopyTo(buffer, OFFSET_PAYLOAD);

        return new ArraySegment<byte>(buffer);
    }

    public Packet Deserialize(ArraySegment<byte> data)
    {
        return Parse(data);
    }

    private Packet Parse(ArraySegment<byte> data)
    {
        if (data.Count < HEADER_LENGTH)
            throw new InvalidDataException($"{nameof(data)} does not begin with a valid header! Expected bytes: ({HEADER_LENGTH}), but it was: ({data.Count}).");

        byte sequence = data[OFFSET_SEQUENCE];
        ushort id = BitConverter.ToUInt16(data.Array, data.Offset + OFFSET_ID);
        ushort length = BitConverter.ToUInt16(data.Array, data.Offset + OFFSET_LENGTH);

        int actualLength = data.Count - HEADER_LENGTH;
        if (length != actualLength)
            throw new InvalidDataException($"{nameof(data)} does not have a valid payload! Expected bytes: ({length}), but it was: ({actualLength}).");

        ArraySegment<byte> payload = new ArraySegment<byte>(data.Array, data.Offset + HEADER_LENGTH, length);

        return new Packet(sequence, id, payload);
    }
}
