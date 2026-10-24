using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Networking.Rpc;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Skills;

namespace WaywardBeyond.Server.Interactions;

/// <summary>
/// React stage: the authoritative skill progression. Resolves the XP each skill grants for the involved
/// brick, accumulates it into the player's transient <see cref="SkillStateComponent"/>, and pushes the
/// authoritative totals and level-ups to the acting client. The server persists no skill data.
/// </summary>
public sealed class SkillXpHandler(
    ISkillDatabase skillDatabase,
    ServerPeers peers,
    ILogger<SkillXpHandler> logger
) : IInteractionHandler
{
    /// <inheritdoc/>
    public InteractionHandlerFilter Filter => default;

    /// <inheritdoc/>
    public InteractionDecision Handle(ref InteractionContext context)
    {
        if (context.Resolution.Action == InteractionAction.None)
        {
            return InteractionDecision.Continue;
        }

        if (!context.Store.TryGet(context.Player, out SkillStateComponent state))
        {
            return InteractionDecision.Continue;
        }

        bool isBreak = context.Resolution.Action == InteractionAction.Break;
        XPSource source = isBreak ? XPSource.Break : XPSource.Place;
        ushort brickDataID = context.Resolution.Voxel.ID;

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

            SendOrLog(context.ClientId, new SkillStateUpdateMessage
            {
                SkillId = skill.ID,
                TotalXP = totalXP,
                Level = currentLevel.Level,
                XPIntoLevel = currentLevel.XPIntoLevel,
                GainedXP = gainedXP.Value,
            });

            SendXpBar(context.ClientId, skill, currentLevel);

            if (previousLevel.Level != currentLevel.Level)
            {
                SendLevelUp(context.ClientId, skill, previousLevel.Level, currentLevel.Level);
                logger.LogDebug(
                    "Skill \"{skill}\" for player {player} leveled from {previous} to {level}.",
                    skill.ID, context.Player, previousLevel.Level, currentLevel.Level);
            }
        }

        if (mutated)
        {
            context.Store.AddOrUpdate(context.Player, state);
        }

        return InteractionDecision.Continue;
    }

    /// <summary>Displays the XP progress bar for a granted skill. The client formats the message itself.</summary>
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
