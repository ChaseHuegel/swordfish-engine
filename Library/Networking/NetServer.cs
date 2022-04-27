using System;

using Swordfish.Library.Extensions;

namespace Swordfish.Library.Networking
{
    public class NetServer : NetController
    {
        public NetServer(int port) : base(port)
        {
            Console.WriteLine($"Server started on {this.Session.EndPoint}");

#if DEBUG
            this.PacketSent += OnPacketSent;
            this.PacketReceived += OnPacketReceived;
            this.PacketAccepted += OnPacketAccepted;
            this.PacketRejected += OnPacketRejected;
            this.PacketUnknown += OnPacketUnknown;
            this.SessionStarted += OnSessionStarted;
            this.SessionEnded += OnSessionEnded;
            this.SessionRejected += OnSessionRejected;
#endif
        }

#if DEBUG
        public void OnPacketSent(object sender, NetEventArgs e)
        {
            Console.WriteLine($"server->sent {e.PacketID} to {e.EndPoint}");
        }

        public void OnPacketReceived(object sender, NetEventArgs e)
        {
            Console.WriteLine($"server->recieve {e.PacketID} from {e.EndPoint}");
        }

        public void OnPacketAccepted(object sender, NetEventArgs e)
        {
            Console.WriteLine($"server->accept {e.PacketID} from {e.EndPoint}");
        }

        public void OnPacketRejected(object sender, NetEventArgs e)
        {
            Console.WriteLine($"server->reject {e.PacketID} from {e.EndPoint}");
        }

        public void OnPacketUnknown(object sender, NetEventArgs e)
        {
            Console.WriteLine($"server->unknown '{e.Packet.ToString().TruncateUpTo(24).Append("[...]")}' from {e.EndPoint}");
        }

        public void OnSessionStarted(object sender, NetEventArgs e)
        {
            Console.WriteLine($"server->session [{e.Session}] joined from {e.EndPoint}");
        }

        public void OnSessionRejected(object sender, NetEventArgs e)
        {
            Console.WriteLine($"server->session rejected from {e.EndPoint}");
        }

        public void OnSessionEnded(object sender, NetEventArgs e)
        {
            Console.WriteLine($"server->session [{e.Session}] left from {e.EndPoint}");
        }
#endif
    }
}
