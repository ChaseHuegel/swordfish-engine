using System;
using System.Collections.Generic;

using Needlefish;
using Needlefish.Types;

using Swordfish.Library.Networking;
using Swordfish.Library.Networking.Attributes;
using Swordfish.Library.Networking.Packets;

using Xunit;
using Xunit.Abstractions;

namespace Swordfish.Tests
{
    public class NetTests
    {
        private readonly ITestOutputHelper Output;
        public NetTests(ITestOutputHelper testOutputHelper)
        {
            Output = testOutputHelper;
        }

        [Fact]
        public void SerializedPacketDoesDeserialize()
        {
            HandshakePacket packet = new HandshakePacket {
                Signature = "test"
            };

            byte[] buffer = NeedlefishFormatter.Serialize(packet);
            HandshakePacket deserializedPacket = (HandshakePacket) NeedlefishFormatter.Deserialize(typeof(HandshakePacket), buffer);

            Assert.Equal(packet.Signature, deserializedPacket.Signature);
        }
    }
}
