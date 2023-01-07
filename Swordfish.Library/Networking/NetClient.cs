using System;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Extensions;
using Swordfish.Library.Networking.Packets;

namespace Swordfish.Library.Networking
{
    public class NetClient : NetController
    {
        public NetClient(NetControllerSettings settings) : base(settings)
            => Initialize();

        public NetClient(Host host) : base(host)
            => Initialize();

        private void Initialize()
        {
            Debugger.Log($"Client started on {this.Session.EndPoint}");

            this.PacketSent += OnPacketSent;
            this.PacketReceived += OnPacketReceived;
            this.PacketAccepted += OnPacketAccepted;
            this.PacketRejected += OnPacketRejected;
            this.PacketUnknown += OnPacketUnknown;
        }

        public void Handshake()
        {
            Send(HandshakePacket.New);
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
            Debugger.Log($"client->reject {e.PacketID} from {e.EndPoint}", LogType.WARNING);
        }

        public virtual void OnPacketUnknown(object sender, NetEventArgs e)
        {
            Debugger.Log($"client->unknown '{e.Packet.ToString().TruncateUpTo(60).Append("[...]")}' from {e.EndPoint}", LogType.WARNING);
        }
    }
}
