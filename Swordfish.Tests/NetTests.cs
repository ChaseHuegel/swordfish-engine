using Needlefish;
using Swordfish.Library.Networking;
using Xunit;
using Xunit.Abstractions;

namespace Swordfish.Tests;

public class NetTests : TestBase
{
    public NetTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public void SerializedPacketDoesDeserialize()
    {
        Handshake.BeginPacket packet = new Handshake.BeginPacket
        {
            Secret = "test"
        };

        byte[] buffer = NeedlefishFormatter.Serialize(packet);
        Handshake.BeginPacket deserializedPacket = (Handshake.BeginPacket)NeedlefishFormatter.Deserialize(typeof(Handshake.BeginPacket), buffer);

        Assert.Equal(packet.Secret, deserializedPacket.Secret);
    }
}
