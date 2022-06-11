using System;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Extensions;
using Swordfish.Library.Networking.Packets;

namespace Swordfish.Library.Networking
{
    public class NetClient : NetController
    {
        public NetClient(Host host) : base(host)
        {
            Debug.Log($"Client started on {this.Session.EndPoint}");

            this.PacketSent += OnPacketSent;
            this.PacketReceived += OnPacketReceived;
            this.PacketAccepted += OnPacketAccepted;
            this.PacketRejected += OnPacketRejected;
            this.PacketUnknown += OnPacketUnknown;
        }

        public void Handshake()
        {
            Send(new HandshakePacket {
                Signature = HandshakePacket.ValidationSignature
            });
        }

        public virtual void OnPacketSent(object sender, NetEventArgs e)
        {
        }

        public virtual void OnPacketReceived(object sender, NetEventArgs e)
        {
        }

        public virtual void OnPacketAccepted(object sender, NetEventArgs e)
        {
        }

        public virtual void OnPacketRejected(object sender, NetEventArgs e)
        {
            Debug.Log($"client->reject {e.PacketID} from {e.EndPoint}", LogType.WARNING);
        }

        public virtual void OnPacketUnknown(object sender, NetEventArgs e)
        {
            Debug.Log($"client->unknown '{e.Packet.ToString().TruncateUpTo(60).Append("[...]")}' from {e.EndPoint}", LogType.WARNING);
        }
    }
}
