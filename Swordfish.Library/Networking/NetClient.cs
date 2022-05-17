using System;

using Swordfish.Library.Extensions;
using Swordfish.Library.Networking.Packets;

namespace Swordfish.Library.Networking
{
    public class NetClient : NetController
    {
        public NetClient(Host host) : base(host)
        {
            Console.WriteLine($"Client started on {this.Session.EndPoint}");

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
            Console.WriteLine($"client->sent {e.PacketID} to {e.EndPoint}");
        }

        public virtual void OnPacketReceived(object sender, NetEventArgs e)
        {
            Console.WriteLine($"client->recieve {e.PacketID} from {e.EndPoint}");
        }

        public virtual void OnPacketAccepted(object sender, NetEventArgs e)
        {
            Console.WriteLine($"client->accept {e.PacketID} from {e.EndPoint}");
        }

        public virtual void OnPacketRejected(object sender, NetEventArgs e)
        {
            Console.WriteLine($"client->reject {e.PacketID} from {e.EndPoint}");
        }

        public virtual void OnPacketUnknown(object sender, NetEventArgs e)
        {
            Console.WriteLine($"client->unknown '{e.Packet.ToString().TruncateUpTo(24).Append("[...]")}' from {e.EndPoint}");
        }
    }
}
