using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Networking;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Networking.Rpc;
using WaywardBeyond.Skills;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// The authoritative skill progression engine. Called by <see cref="ServerInteractionSystem"/> after an
/// accepted break/place interaction, it resolves the XP each <see cref="Skill"/> grants for the involved
/// brick, accumulates it into the player's transient <see cref="SkillStateComponent"/> (seeded from the
/// client's statistics at join), and pushes the authoritative per-skill totals to the acting player's
/// client. Level-ups are detected here; surfacing them (as notifications) happens in
/// <see cref="OnInteractionApplied"/>. The server persists no skill data.
/// </summary>
public sealed class ServerSkillSystem(
    ISkillDatabase skillDatabase,
    SessionManager sessions,
    ServerPeers peers,
    ILogger<ServerSkillSystem> logger
)
{
    /// <summary>
    /// Grants XP for a successfully applied interaction. <paramref name="isBreak"/> maps to
    /// <see cref="XPSource.Break"/>; otherwise the interaction is a place. The state component must exist
    /// on the player mirror (seeded at join); without a bound client the grant is skipped.
    /// </summary>
    public void OnInteractionApplied(DataStore store, int player, ushort brickDataID, bool isBreak)
    {
        if (!store.TryGet(player, out SkillStateComponent state) ||
            !sessions.TryGetClient(player, out Uuid clientId))
        {
            return;
        }

        XPSource source = isBreak ? XPSource.Break : XPSource.Place;

        bool mutated = false;
        foreach (Skill skill in skillDatabase.Get(source))
        {
            Result<int> gainedXP = skill.GetXP(source, brickDataID);
            if (!gainedXP.Success)
            {
                continue;
            }

            long previousXP = state.GetXP(skill.ID);
            long totalXP = previousXP + gainedXP.Value;
            state.SetXP(skill.ID, totalXP);
            mutated = true;

            LevelInfo previousLevel = skill.CalculateLevel(previousXP);
            LevelInfo currentLevel = skill.CalculateLevel(totalXP);

            SendOrLog(clientId, new SkillStateUpdateMessage
            {
                SkillId = skill.ID,
                TotalXP = totalXP,
                Level = currentLevel.Level,
                XPIntoLevel = currentLevel.XPIntoLevel,
                GainedXP = gainedXP.Value,
            });

            SendXpBar(clientId, skill, currentLevel);

            if (previousLevel.Level != currentLevel.Level)
            {
                SendLevelUp(clientId, skill, previousLevel.Level, currentLevel.Level);
                logger.LogDebug("Skill \"{skill}\" for player {player} leveled from {previous} to {level}.", skill.ID, player, previousLevel.Level, currentLevel.Level);
            }
        }

        if (mutated)
        {
            store.AddOrUpdate(player, state);
        }
    }

    /// <summary>
    /// Displays the XP progress bar for a granted skill. The client formats the message from its own
    /// localization; amounts are display-ready.
    /// </summary>
    private void SendXpBar(Uuid clientId, Skill skill, LevelInfo currentLevel)
    {
        int nextLevelXP = skill.Levels.TryGetValue(currentLevel.Level + 1, out int next) ? next : 0;
        float amount = nextLevelXP > 0 ? (float)currentLevel.XPIntoLevel / nextLevelXP : 1f;

        SendOrLog(clientId, new NotificationMessage
        {
            Type = NotificationType.Bar,
            Key = "notification.skill.bar",
            Args = [skill.Name, currentLevel.Level.ToString()],
            ID = skill.ID,
            Amount = amount,
        });
    }

    private void SendLevelUp(Uuid clientId, Skill skill, int previousLevel, int currentLevel)
    {
        SendOrLog(clientId, new NotificationMessage
        {
            Type = NotificationType.Toast,
            Key = "notification.skill.levelUp",
            Args = [skill.Name, previousLevel.ToString(), currentLevel.ToString()],
        });
    }

    private void SendOrLog(Uuid clientId, in SkillStateUpdateMessage message)
    {
        if (peers.TryGet(clientId, out ServerPeer peer) && !peer.Rpc.Notify(GameRpc.Methods.SKILL_UPDATE, in message))
        {
            peer.CloseOnRejectedSend(logger);
        }
    }

    private void SendOrLog(Uuid clientId, in NotificationMessage message)
    {
        if (peers.TryGet(clientId, out ServerPeer peer) && !peer.Rpc.Notify(GameRpc.Methods.NOTIFICATION, in message))
        {
            peer.CloseOnRejectedSend(logger);
        }
    }
}