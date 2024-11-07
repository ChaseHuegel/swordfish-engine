namespace Swordfish.Library.Networking;

public class NetStatsRecord
{
    public ulong BytesIn { get; private set; }
    public ulong BytesAccepted { get; private set; }
    public ulong BytesOut { get; private set; }

    public ulong PacketsSent { get; private set; }
    public ulong PacketsRecieved { get; private set; }
    public ulong PacketsAccepted { get; private set; }
    public ulong PacketsRejected { get; private set; }

    public ulong SessionsStarted { get; private set; }
    public ulong SessionsClosed { get; private set; }
    public ulong SessionsDisconnected { get; private set; }
    public ulong SessionsExpired { get; private set; }
    public ulong SessionsRejected { get; private set; }

    public void RecordBytesIn(int bytes)
    {
        BytesIn += (ulong)bytes;
    }

    public void RecordBytesAccepted(int bytes)
    {
        BytesAccepted += (ulong)bytes;
    }

    public void RecordBytesOut(int bytes)
    {
        BytesOut += (ulong)bytes;
    }

    public void RecordPacketSent()
    {
        PacketsSent++;
    }

    public void RecordPacketRecieved()
    {
        PacketsRecieved++;
    }

    public void RecordPacketAccepted()
    {
        PacketsAccepted++;
    }

    public void RecordPacketRejected()
    {
        PacketsRejected++;
    }

    public void RecordSessionStarted()
    {
        SessionsStarted++;
    }

    public void RecordSessionClosed()
    {
        SessionsClosed++;
    }

    public void RecordSessionDisconnected()
    {
        SessionsDisconnected++;
    }

    public void RecordSessionExpired()
    {
        SessionsExpired++;
    }

    public void RecordSessionRejected()
    {
        SessionsRejected++;
    }

    public NetStatsRecord Clone() => new()
    {
        BytesIn = BytesIn,
        BytesAccepted = BytesAccepted,
        BytesOut = BytesOut,
        PacketsSent = PacketsSent,
        PacketsRecieved = PacketsRecieved,
        PacketsAccepted = PacketsAccepted,
        PacketsRejected = PacketsRejected,
        SessionsStarted = SessionsStarted,
        SessionsClosed = SessionsClosed,
        SessionsDisconnected = SessionsDisconnected,
        SessionsExpired = SessionsExpired,
        SessionsRejected = SessionsRejected,
    };

    public override string ToString()
    {
        return $"In/Accepted: {BytesIn}/{BytesAccepted} Out: {BytesOut}"
               + $"\nSent: {PacketsSent} Received/Accepted/Rejected: {PacketsRecieved}/{PacketsAccepted}/{PacketsRejected}"
               + $"\nSessions (started/ended/expired/closed/rejected): {SessionsStarted}/{SessionsDisconnected}/{SessionsExpired}/{SessionsClosed}/{SessionsRejected}";
    }

    public override bool Equals(object obj)
    {
        return obj is NetStatsRecord record &&
               BytesIn.Equals(record.BytesIn) &&
               BytesAccepted.Equals(record.BytesAccepted) &&
               BytesOut.Equals(record.BytesOut) &&
               PacketsSent.Equals(record.PacketsSent) &&
               PacketsRecieved.Equals(record.PacketsRecieved) &&
               PacketsAccepted.Equals(record.PacketsAccepted) &&
               PacketsRejected.Equals(record.PacketsRejected) &&
               SessionsStarted.Equals(record.SessionsStarted) &&
               SessionsClosed.Equals(record.SessionsClosed) &&
               SessionsDisconnected.Equals(record.SessionsDisconnected) &&
               SessionsExpired.Equals(record.SessionsExpired) &&
               SessionsRejected.Equals(record.SessionsRejected);
    }

    public override int GetHashCode()
    {
        var hashCode = 1537860459;
        hashCode = hashCode * -1521134295 + BytesIn.GetHashCode();
        hashCode = hashCode * -1521134295 + BytesAccepted.GetHashCode();
        hashCode = hashCode * -1521134295 + BytesOut.GetHashCode();
        hashCode = hashCode * -1521134295 + PacketsSent.GetHashCode();
        hashCode = hashCode * -1521134295 + PacketsRecieved.GetHashCode();
        hashCode = hashCode * -1521134295 + PacketsAccepted.GetHashCode();
        hashCode = hashCode * -1521134295 + PacketsRejected.GetHashCode();
        hashCode = hashCode * -1521134295 + SessionsStarted.GetHashCode();
        hashCode = hashCode * -1521134295 + SessionsClosed.GetHashCode();
        hashCode = hashCode * -1521134295 + SessionsDisconnected.GetHashCode();
        hashCode = hashCode * -1521134295 + SessionsExpired.GetHashCode();
        hashCode = hashCode * -1521134295 + SessionsRejected.GetHashCode();
        return hashCode;
    }

    public static bool operator ==(NetStatsRecord left, NetStatsRecord right) => left.Equals(right);

    public static bool operator !=(NetStatsRecord left, NetStatsRecord right) => !left.Equals(right);

    public static NetStatsRecord operator +(NetStatsRecord left, NetStatsRecord right) => new()
    {
        BytesIn = left.BytesIn + right.BytesIn,
        BytesAccepted = left.BytesAccepted + right.BytesAccepted,
        BytesOut = left.BytesOut + right.BytesOut,
        PacketsSent = left.PacketsSent + right.PacketsSent,
        PacketsRecieved = left.PacketsRecieved + right.PacketsRecieved,
        PacketsAccepted = left.PacketsAccepted + right.PacketsAccepted,
        PacketsRejected = left.PacketsRejected + right.PacketsRejected,
        SessionsStarted = left.SessionsStarted + right.SessionsStarted,
        SessionsClosed = left.SessionsClosed + right.SessionsClosed,
        SessionsDisconnected = left.SessionsDisconnected + right.SessionsDisconnected,
        SessionsExpired = left.SessionsExpired + right.SessionsExpired,
        SessionsRejected = left.SessionsRejected + right.SessionsRejected,
    };

    public static NetStatsRecord operator -(NetStatsRecord left, NetStatsRecord right) => new()
    {
        BytesIn = left.BytesIn - right.BytesIn,
        BytesAccepted = left.BytesAccepted - right.BytesAccepted,
        BytesOut = left.BytesOut - right.BytesOut,
        PacketsSent = left.PacketsSent - right.PacketsSent,
        PacketsRecieved = left.PacketsRecieved - right.PacketsRecieved,
        PacketsAccepted = left.PacketsAccepted - right.PacketsAccepted,
        PacketsRejected = left.PacketsRejected - right.PacketsRejected,
        SessionsStarted = left.SessionsStarted - right.SessionsStarted,
        SessionsClosed = left.SessionsClosed - right.SessionsClosed,
        SessionsDisconnected = left.SessionsDisconnected - right.SessionsDisconnected,
        SessionsExpired = left.SessionsExpired - right.SessionsExpired,
        SessionsRejected = left.SessionsRejected - right.SessionsRejected,
    };


    public static NetStatsRecord operator *(NetStatsRecord left, NetStatsRecord right) => new()
    {
        BytesIn = left.BytesIn * right.BytesIn,
        BytesAccepted = left.BytesAccepted * right.BytesAccepted,
        BytesOut = left.BytesOut * right.BytesOut,
        PacketsSent = left.PacketsSent * right.PacketsSent,
        PacketsRecieved = left.PacketsRecieved * right.PacketsRecieved,
        PacketsAccepted = left.PacketsAccepted * right.PacketsAccepted,
        PacketsRejected = left.PacketsRejected * right.PacketsRejected,
        SessionsStarted = left.SessionsStarted * right.SessionsStarted,
        SessionsClosed = left.SessionsClosed * right.SessionsClosed,
        SessionsDisconnected = left.SessionsDisconnected * right.SessionsDisconnected,
        SessionsExpired = left.SessionsExpired * right.SessionsExpired,
        SessionsRejected = left.SessionsRejected * right.SessionsRejected,
    };


    public static NetStatsRecord operator /(NetStatsRecord left, NetStatsRecord right) => new()
    {
        BytesIn = left.BytesIn / right.BytesIn,
        BytesAccepted = left.BytesAccepted / right.BytesAccepted,
        BytesOut = left.BytesOut / right.BytesOut,
        PacketsSent = left.PacketsSent / right.PacketsSent,
        PacketsRecieved = left.PacketsRecieved / right.PacketsRecieved,
        PacketsAccepted = left.PacketsAccepted / right.PacketsAccepted,
        PacketsRejected = left.PacketsRejected / right.PacketsRejected,
        SessionsStarted = left.SessionsStarted / right.SessionsStarted,
        SessionsClosed = left.SessionsClosed / right.SessionsClosed,
        SessionsDisconnected = left.SessionsDisconnected / right.SessionsDisconnected,
        SessionsExpired = left.SessionsExpired / right.SessionsExpired,
        SessionsRejected = left.SessionsRejected / right.SessionsRejected,
    };
}