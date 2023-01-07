using System;
using System.Net;
using System.Timers;
using Swordfish.Library.Networking.Packets;

namespace Swordfish.Library.Networking
{
    public class NetSession
    {
        /// <summary>
        /// The ID of a local or otherwise unassigned session.
        /// </summary>
        public const int LocalOrUnassigned = 0;

        public IPEndPoint EndPoint { get; set; }

        public int ID { get; set; }

        public NetController Controller { get; private set; }

        private Timer ExpirationTimer;

        public NetSession(NetController controller)
        {
            Controller = controller;
            RefreshExpiration();
        }

        public bool IsValid() => ID != LocalOrUnassigned && Controller != null;

        public void RefreshExpiration()
        {
            //  Local and orphan sessions can't expire
            if (!IsValid())
                return;

            if (Controller.SessionExpiration.TotalMilliseconds > 0)
            {
                if (ExpirationTimer == null)
                {
                    ExpirationTimer = new Timer();
                    ExpirationTimer.Elapsed += OnSessionElapsed;
                }

                ExpirationTimer.Interval = Controller.SessionExpiration.TotalMilliseconds;
                ExpirationTimer.Enabled = true;
            }
            else if (ExpirationTimer != null)
            {
                ExpirationTimer.Dispose();
                ExpirationTimer = null;
            }
        }

        public override string ToString()
        {
            return $"{ID}/{EndPoint}";
        }

        public override bool Equals(object obj)
        {
            return obj is NetSession other && this.ID == other.ID && (this?.EndPoint.Equals(other?.EndPoint) ?? false);
        }

        public override int GetHashCode()
        {
            return ID.GetHashCode() ^ EndPoint.GetHashCode();
        }

        private void OnSessionElapsed(object sender, ElapsedEventArgs e)
        {
            //  Local and orphan sessions can't expire
            if (!IsValid())
                return;

            Controller.TryRemoveSession(this);
        }
    }
}
