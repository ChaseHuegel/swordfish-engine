using Swordfish.Library.Util;
using Torches.Networking.Models;

namespace WaywardBeyond.Server.Core.Networking;

internal sealed class ProtocolV1 : IProtocol
{
    public Result Send(Packet packet)
    {
        return Result.FromSuccess();
    }
}