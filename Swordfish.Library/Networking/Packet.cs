using System;
using Needlefish;

namespace Swordfish.Library.Networking
{
    public class Packet
    {
        /// <returns>instance of <typeparamref name="Packet"/> </returns>
        public static Packet Create(int sessionID, int packetID, uint sequence, IDataBody source)
        {
            Packet packet = new Packet {
                SessionID = sessionID,
                PacketID = packetID,
                Sequence = sequence,
                Data = NeedlefishFormatter.Serialize(source)
            };

            return packet;
        }

        public int SessionID { get; set; }
        public int PacketID { get; set; }
        public uint Sequence { get; set; }
        public byte[] Data { get; set; }

        private byte[] Buffer;

        public Packet() {}

        public Packet(byte[] buffer)
        {
            Buffer = buffer;
        }

        public static implicit operator Packet(byte[] data) => new Packet(data);
        public static implicit operator byte[](Packet packet) => packet.Buffer;

        public byte[] Pack()
        {
            Buffer = new byte[12 + Data.Length];
            BitConverter.GetBytes(SessionID).CopyTo(Buffer, 0);
            BitConverter.GetBytes(PacketID).CopyTo(Buffer, 4);
            BitConverter.GetBytes(Sequence).CopyTo(Buffer, 8);
            Data.CopyTo(Buffer, 12);
            return Buffer;
        }

        public void Unpack()
        {
            Data = new byte[Buffer.Length - 12];
            SessionID = BitConverter.ToInt32(Buffer, 0);
            PacketID = BitConverter.ToInt32(Buffer, 4);
            Sequence = BitConverter.ToUInt32(Buffer, 8);
            Array.Copy(Buffer, 12, Data, 0, Buffer.Length - 12);
        }

        public void PeakHeaders()
        {
            SessionID = BitConverter.ToInt32(Buffer, 0);
            PacketID = BitConverter.ToInt32(Buffer, 4);
            Sequence = BitConverter.ToUInt32(Buffer, 8);
        }
    }
}
