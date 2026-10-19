namespace WaywardBeyond.Networking.Rpc;

/// <summary>
/// The game wire contract for nsd-rpc. Method ids are stable: never reuse or renumber one between
/// builds. Ranges group methods by subject. The catalogue lives in <c>docs/specs/networking-rpc.md</c>.
/// </summary>
public static class GameRpc
{
    /// <summary>The largest payload bytes one fragment carries, below the RPC frame cap with headroom.</summary>
    public const int FragmentBudget = 60_000;

    /// <summary>Method ids grouped by subject. Each id is a wire contract.</summary>
    public static class Methods
    {
        //  Connection: hello, leave, client heartbeat.
        public const ulong ClientHello = 1;
        public const ulong LeaveGame = 2;
        public const ulong ClientHeartbeat = 3;

        //  Replication: client-owned state upstream.
        public const ulong ClientSnapshot = 20;

        //  Gameplay commands.
        public const ulong Chat = 60;

        //  Menu: level listing and management.
        public const ulong ListLevels = 100;
        public const ulong CreateLevel = 101;
        public const ulong DeleteLevel = 102;
        public const ulong SaveLevel = 103;

        //  Join: session join and the level stream.
        public const ulong Join = 200;
        public const ulong StreamLevel = 201;

        //  Server state: authoritative snapshots.
        public const ulong WorldSnapshot = 300;
        public const ulong SnapshotBatch = 301;

        //  Server events: edits, notifications, skills, chat relay, heartbeat.
        public const ulong VoxelEdit = 350;
        public const ulong Notification = 351;
        public const ulong SkillUpdate = 352;
        public const ulong ChatRelay = 353;
        public const ulong ServerHeartbeat = 354;
    }
}
