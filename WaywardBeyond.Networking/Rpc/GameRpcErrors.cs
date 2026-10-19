namespace WaywardBeyond.Networking.Rpc;

/// <summary>
/// Application error codes carried in <c>RpcResult&lt;T&gt;.ErrorCode</c>. The nsd-rpc app range starts
/// at <c>RpcErrorCodes.AppCodeMin</c> (-32100) and goes down. The wire carries the code and the
/// message, never an exception.
/// </summary>
public static class GameRpcErrors
{
    public const int NotJoined = -32100;
    public const int JoinRefused = -32101;
    public const int LevelNotFound = -32102;
    public const int LevelLoadFailed = -32103;
    public const int PermissionDenied = -32104;
    public const int InvalidState = -32105;
    public const int StreamFailed = -32106;
}
