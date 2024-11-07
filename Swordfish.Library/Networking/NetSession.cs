using System.Net;
using System.Timers;

namespace Swordfish.Library.Networking;

public class NetSession
{
    /// <summary>
    /// The ID of a local or otherwise unassigned session.
    /// </summary>
    public const int LOCAL_OR_UNASSIGNED = 0;

    public IPEndPoint EndPoint { get; set; }

    public int ID { get; set; }

    public NetController Controller { get; private set; }

    private Timer _expirationTimer;

    public NetSession(NetController controller)
    {
        Controller = controller;
        RefreshExpiration();
    }

    public bool IsValid() => ID != LOCAL_OR_UNASSIGNED && Controller != null;

    public void RefreshExpiration()
    {
        //  Local and orphan sessions can't expire
        if (!IsValid())
        {
            return;
        }

        if (Controller.SessionExpiration.TotalMilliseconds > 0)
        {
            if (_expirationTimer == null)
            {
                _expirationTimer = new Timer(Controller.SessionExpiration.TotalMilliseconds);
                _expirationTimer.Elapsed += OnSessionElapsed;
            }

            _expirationTimer.Stop();
            _expirationTimer.Start();
        }
    }

    public override string ToString()
    {
        return $"{ID}/{EndPoint}";
    }

    public override bool Equals(object obj)
    {
        return obj is NetSession other && ID == other.ID && (this?.EndPoint.Equals(other?.EndPoint) ?? false);
    }

    public override int GetHashCode()
    {
        return ID.GetHashCode() ^ EndPoint.GetHashCode();
    }

    private void OnSessionElapsed(object sender, ElapsedEventArgs e)
    {
        _expirationTimer.Stop();
        Controller.TryRemoveSession(this, SessionEndedReason.EXPIRED);
    }
}