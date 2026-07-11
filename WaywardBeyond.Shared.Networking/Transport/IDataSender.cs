using Swordfish.Library.Collections.Filtering;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Networking.Sessions;

namespace WaywardBeyond.Shared.Networking.Transport;

public interface IDataSender
{
    Result Send(byte[] data, Session target);
    Result Send(byte[] data, IFilter<Session> targetFilter);
}
