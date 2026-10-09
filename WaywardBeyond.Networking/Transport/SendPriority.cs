using System;
using System.Collections.Generic;
using WaywardBeyond.Data;

namespace WaywardBeyond.Networking.Transport;

/// <summary>
/// Classifies message types into the transport's two send queues: control/state messages ride the
/// never-evicting reliable queue; per-tick snapshot traffic rides the bounded droppable queue. The
/// allow-list is explicit so a new per-tick message must be deliberately marked droppable to share the
/// snapshot queue.
/// </summary>
public static class SendPriority
{
    private static readonly HashSet<Type> _reliable = new()
    {
        typeof(JoinRequest),
        typeof(JoinAccept),
        typeof(LevelStreamComplete),
        typeof(LeaveGameRequest),
        typeof(LevelEntityAdd),
        typeof(SaveLevelRequest),
        typeof(SaveLevelResponse),
        typeof(NewLevelRequest),
        typeof(NewLevelResponse),
        typeof(ListLevelsRequest),
        typeof(ListLevelsResponse),
        typeof(DeleteLevelRequest),
        typeof(DeleteLevelResponse),
        typeof(ChatMessage),
        typeof(VoxelEditMessage),
        typeof(NotificationMessage),
        typeof(SkillStateUpdateMessage),
        typeof(ServerHeartbeatMessage),
        typeof(ClientHeartbeatMessage),
    };

    /// <summary>True for control/state messages that must never be silently dropped.</summary>
    public static bool IsReliable(Type type)
    {
        return _reliable.Contains(type);
    }
}