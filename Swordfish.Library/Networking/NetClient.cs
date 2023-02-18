using System;
using System.Timers;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Extensions;
using Swordfish.Library.Networking.Packets;

namespace Swordfish.Library.Networking
{
    public class NetClient : NetController
    {
        private Timer TimeoutTimer;
        private TimeSpan Timeout;

        public NetClient(NetControllerSettings settings, TimeSpan timeout = default) : base(settings)
            => Initialize(timeout);

        public NetClient(Host host, TimeSpan timeout = default) : base(host)
            => Initialize(timeout);

        private void Initialize(TimeSpan timeout)
        {
            Debugger.Log($"Client started on {this.Session.EndPoint}");

            this.PacketSent += OnPacketSent;
            this.PacketReceived += OnPacketReceived;
            this.PacketAccepted += OnPacketAccepted;
            this.PacketRejected += OnPacketRejected;
            this.PacketUnknown += OnPacketUnknown;

            Timeout = timeout == default ? TimeSpan.FromSeconds(30) : timeout;
            TimeoutTimer = new Timer(Timeout.TotalMilliseconds)
            {
                AutoReset = false
            };

            TimeoutTimer.Elapsed += OnTimeout;
            TimeoutTimer.Start();
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
            TimeoutTimer.Stop();
            TimeoutTimer.Start();
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

        private void OnTimeout(object sender, ElapsedEventArgs e)
        {
            Disconnect();
        }
    }
}
