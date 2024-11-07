using System;
using System.Timers;

namespace Swordfish.Library.Networking;

public class NetStatsService
{
    public NetStatsRecord Record { get; private set; } = new();

    public void RequestInterval(TimeSpan timeSpan, Action<NetStatsRecord> callback)
    {
        NetStatsRecord start = Record.Clone();

        var timer = new Timer(timeSpan.TotalMilliseconds);
        timer.Elapsed += OnElapsed;
        timer.Start();

        void OnElapsed(object sender, ElapsedEventArgs e)
        {
            NetStatsRecord end = Record.Clone();
            NetStatsRecord intervalRecord = end - start;
            callback?.Invoke(intervalRecord);
            timer.Dispose();
        }
    }

    public void RecordBytesIn(int bytes) => Record.RecordBytesIn(bytes);
    public void RecordBytesAccepted(int bytes) => Record.RecordBytesAccepted(bytes);
    public void RecordBytesOut(int bytes) => Record.RecordBytesOut(bytes);

    public void RecordPacketSent() => Record.RecordPacketSent();
    public void RecordPacketRecieved() => Record.RecordPacketRecieved();
    public void RecordPacketAccepted() => Record.RecordPacketAccepted();
    public void RecordPacketRejected() => Record.RecordPacketRejected();

    public void RecordSessionStarted() => Record.RecordSessionStarted();
    public void RecordSessionClosed() => Record.RecordSessionClosed();
    public void RecordSessionDisconnected() => Record.RecordSessionDisconnected();
    public void RecordSessionExpired() => Record.RecordSessionExpired();
    public void RecordSessionRejected() => Record.RecordSessionRejected();
}