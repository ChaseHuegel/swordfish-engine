namespace WaywardBeyond.Networking.Rpc;

/// <summary>
/// Application error codes carried in nsd-rpc errors.
/// The nsd-rpc app range starts at <c>RpcErrorCodes.AppCodeMin</c> (-32100) and goes down.
/// The wire carries the code and the message. It does not expose exception details.
/// </summary>
public static class GameRpcErrors
{
    public const int NOT_JOINED = -32100;
    public const int JOIN_REFUSED = -32101;
    public const int LEVEL_NOT_FOUND = -32102;
    public const int LEVEL_LOAD_FAILED = -32103;
    public const int PERMISSION_DENIED = -32104;
    public const int INVALID_STATE = -32105;
    public const int STREAM_FAILED = -32106;
}
