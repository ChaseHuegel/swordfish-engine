using Swordfish.Library.Util;
using Torches.Networking.Models;

namespace WaywardBeyond.Server.Core.Networking;

internal interface IProtocol
{
    Result Send(Packet packet);
}