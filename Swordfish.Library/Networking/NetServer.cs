using System;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Extensions;

namespace Swordfish.Library.Networking
{
    public class NetServer : NetController
    {
        public NetServer(NetControllerSettings settings) : base(settings)
            => Initialize();

        public NetServer(int port) : base(port)
            => Initialize();

        private void Initialize()
        {
            Debugger.Log($"Server started on {this.Session.EndPoint}");

            this.PacketSent += OnPacketSent;
            this.PacketReceived += OnPacketReceived;
            this.PacketAccepted += OnPacketAccepted;
            this.PacketRejected += OnPacketRejected;
            this.PacketUnknown += OnPacketUnknown;
            this.SessionStarted += OnSessionStarted;
            this.SessionEnded += OnSessionEnded;
            this.SessionRejected += OnSessionRejected;
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
            Debugger.Log($"server->reject {e.PacketID} from {e.EndPoint}", LogType.WARNING);
        }

        public virtual void OnPacketUnknown(object sender, NetEventArgs e)
        {
            Debugger.Log($"server->unknown '{e.Packet.ToString().TruncateUpTo(60).Append("[...]")}' from {e.EndPoint}", LogType.WARNING);
        }

        public virtual void OnSessionStarted(object sender, NetEventArgs e)
        {
            Debugger.Log($"server->session [{e.Session}] connected from {e.EndPoint}");
        }

        public virtual void OnSessionRejected(object sender, NetEventArgs e)
        {
            Debugger.Log($"server->session rejected from {e.EndPoint}", LogType.WARNING);
        }

        public virtual void OnSessionEnded(object sender, NetEventArgs e)
        {
            Debugger.Log($"server->session [{e.Session}] disconnected from {e.EndPoint}");
        }
    }
}
