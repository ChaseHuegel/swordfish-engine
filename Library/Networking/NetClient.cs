using System;

using Swordfish.Library.Extensions;

namespace Swordfish.Library.Networking
{
    public class NetClient : NetController
    {
        public NetClient(Host host) : base(host)
        {
            Console.WriteLine($"Client started on {this.Session.EndPoint}");

#if DEBUG
            this.PacketSent += OnPacketSent;
            this.PacketReceived += OnPacketReceived;
            this.PacketAccepted += OnPacketAccepted;
            this.PacketRejected += OnPacketRejected;
            this.PacketUnknown += OnPacketUnknown;
#endif
        }

#if DEBUG
        public void OnPacketSent(object sender, NetEventArgs e)
        {
            Console.WriteLine($"client->sent {e.PacketID} to {e.EndPoint}");
        }

        public void OnPacketReceived(object sender, NetEventArgs e)
        {
            Console.WriteLine($"client->recieve {e.PacketID} from {e.EndPoint}");
        }

        public void OnPacketAccepted(object sender, NetEventArgs e)
        {
            Console.WriteLine($"client->accept {e.PacketID} from {e.EndPoint}");
        }

        public void OnPacketRejected(object sender, NetEventArgs e)
        {
            Console.WriteLine($"client->reject {e.PacketID} from {e.EndPoint}");
        }

        public void OnPacketUnknown(object sender, NetEventArgs e)
        {
            Console.WriteLine($"client->unknown '{e.Packet.ToString().TruncateUpTo(24).Append("[...]")}' from {e.EndPoint}");
        }
#endif
    }
}
