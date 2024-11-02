using System;
using System.Timers;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Extensions;

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
            this.PacketSent += OnPacketSent;
            this.PacketReceived += OnPacketReceivedInternal;
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

        private void OnPacketReceivedInternal(object sender, NetEventArgs e)
        {
            TimeoutTimer.Stop();
            TimeoutTimer.Start();
            OnPacketReceived(sender, e);
        }

        protected virtual void OnPacketSent(object sender, NetEventArgs e)
        {
        }

        protected virtual void OnPacketReceived(object sender, NetEventArgs e)
        {
        }

        protected virtual void OnPacketAccepted(object sender, NetEventArgs e)
        {
        }

        protected virtual void OnPacketRejected(object sender, NetEventArgs e)
        {
        }

        protected virtual void OnPacketUnknown(object sender, NetEventArgs e)
        {
        }

        private void OnTimeout(object sender, ElapsedEventArgs e)
        {
            Disconnect();
        }
    }
}
