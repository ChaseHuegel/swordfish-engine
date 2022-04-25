using System;
using Swordfish.Library.Networking.Packets;

namespace Swordfish.Library.Networking
{
    public class Server : NetController
    {
        public Server(int port) : base(port)
        {
            Console.WriteLine($"Server started on {this.Session.EndPoint}");

            this.PacketSent += OnPacketSent;
            this.PacketReceived += OnPacketReceived;
            this.PacketAccepted += OnPacketAccepted;
            this.PacketRejected += OnPacketRejected;
            this.SessionStarted += OnSessionStarted;
            this.SessionEnded += OnSessionEnded;
            this.SessionRejected += OnSessionRejected;
        }

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
    }
}
