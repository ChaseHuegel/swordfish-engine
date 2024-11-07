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
            this.PacketSent += OnPacketSent;
            this.PacketReceived += OnPacketReceived;
            this.PacketAccepted += OnPacketAccepted;
            this.PacketRejected += OnPacketRejected;
            this.PacketUnknown += OnPacketUnknown;
            this.SessionStarted += OnSessionStarted;
            this.SessionEnded += OnSessionEnded;
            this.SessionRejected += OnSessionRejected;
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

        protected virtual void OnSessionStarted(object sender, NetEventArgs e)
        {
        }

        protected virtual void OnSessionRejected(object sender, NetEventArgs e)
        {
        }

        protected virtual void OnSessionEnded(object sender, NetEventArgs e)
        {
        }
    }
}
