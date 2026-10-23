namespace WaywardBeyond.Networking.Rpc;

/// <summary>The outcome of appending one fragment to a group.</summary>
public enum FragmentState : byte
{
    /// <summary>The group is incomplete.</summary>
    Incomplete = 0,

    /// <summary>The group completed and the payload was produced.</summary>
    Complete = 1,

    /// <summary>The part violated the group contract and the group was dropped.</summary>
    Rejected = 2,
}
