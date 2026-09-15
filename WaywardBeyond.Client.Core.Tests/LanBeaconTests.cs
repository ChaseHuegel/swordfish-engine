using WaywardBeyond.Shared.Networking;

namespace WaywardBeyond.Client.Core.Tests;

public class LanBeaconTests
{
    [Test]
    public void SerializeRoundTripPreservesFields()
    {
        var beacon = new LanBeacon
        {
            ServerName = "Test Server",
            TcpPort = 7777,
            ProtocolVersion = 1,
            PlayerCount = 3,
        };

        byte[] payload = beacon.Serialize();
        LanBeacon decoded = LanBeacon.Deserialize(payload);

        Assert.That(decoded.ServerName, Is.EqualTo(beacon.ServerName));
        Assert.That(decoded.TcpPort, Is.EqualTo(beacon.TcpPort));
        Assert.That(decoded.ProtocolVersion, Is.EqualTo(beacon.ProtocolVersion));
        Assert.That(decoded.PlayerCount, Is.EqualTo(beacon.PlayerCount));
    }

    [Test]
    public void SerializeRoundTripPreservesEmptyName()
    {
        var beacon = new LanBeacon
        {
            ServerName = string.Empty,
            TcpPort = 1,
            ProtocolVersion = 1,
            PlayerCount = 0,
        };

        LanBeacon decoded = LanBeacon.Deserialize(beacon.Serialize());

        Assert.That(decoded.ServerName, Is.EqualTo(string.Empty));
        Assert.That(decoded.PlayerCount, Is.Zero);
    }
}