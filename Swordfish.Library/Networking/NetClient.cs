using System;
using System.Timers;

namespace Swordfish.Library.Networking;

public class NetClient : NetController
{
    private Timer _timeoutTimer;
    private TimeSpan _timeout;

    public NetClient(NetControllerSettings settings, TimeSpan timeout = default) : base(settings)
        => Initialize(timeout);

    public NetClient(Host host, TimeSpan timeout = default) : base(host)
        => Initialize(timeout);

    private void Initialize(TimeSpan timeout)
    {
        PacketSent += OnPacketSent;
        PacketReceived += OnPacketReceivedInternal;
        PacketAccepted += OnPacketAccepted;
        PacketRejected += OnPacketRejected;
        PacketUnknown += OnPacketUnknown;

        _timeout = timeout == default ? TimeSpan.FromSeconds(30) : timeout;
        _timeoutTimer = new Timer(_timeout.TotalMilliseconds)
        {
            AutoReset = false,
        };

        _timeoutTimer.Elapsed += OnTimeout;
        _timeoutTimer.Start();
    }

    private void OnPacketReceivedInternal(object sender, NetEventArgs e)
    {
        _timeoutTimer.Stop();
        _timeoutTimer.Start();
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