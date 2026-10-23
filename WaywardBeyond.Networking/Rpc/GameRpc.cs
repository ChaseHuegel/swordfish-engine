namespace WaywardBeyond.Networking.Rpc;

/// <summary>
/// The game wire contract for nsd-rpc.
/// Method ids are stable: never reuse or renumber one between builds.
/// Ranges group methods by subject.
/// </summary>
public static class GameRpc
{
    /// <summary>The largest payload bytes one fragment carries, below the RPC frame cap with headroom.</summary>
    public const int FRAGMENT_BUDGET = 60_000;

    /// <summary>Method ids grouped by subject. Each id is a wire contract.</summary>
    public static class Methods
    {
        //  Connection
        public const ulong CLIENT_HELLO = 1;
        public const ulong LEAVE_GAME = 2;
        public const ulong CLIENT_HEARTBEAT = 3;

        //  Replication
        public const ulong CLIENT_SNAPSHOT = 20;

        //  Gameplay commands
        public const ulong CHAT = 60;

        //  Level information
        public const ulong LIST_LEVELS = 100;
        public const ulong CREATE_LEVEL = 101;
        public const ulong DELETE_LEVEL = 102;
        public const ulong SAVE_LEVEL = 103;

        //  Session management
        public const ulong JOIN = 200;
        public const ulong STREAM_LEVEL = 201;

        //  Server state
        public const ulong WORLD_SNAPSHOT = 300;
        public const ulong SNAPSHOT_BATCH = 301;

        //  Server events
        public const ulong VOXEL_EDIT = 350;
        public const ulong NOTIFICATION = 351;
        public const ulong SKILL_UPDATE = 352;
        public const ulong CHAT_RELAY = 353;
        public const ulong SERVER_HEARTBEAT = 354;
    }
}
