using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Transport;
using WaywardBeyond.Shared.Skills;

namespace WaywardBeyond.Server.Core.Systems;

/// <summary>
/// The authoritative skill progression engine. Called by <see cref="ServerInteractionSystem"/> after an
/// accepted break/place interaction, it resolves the XP each <see cref="Shared.Skills.SkillData"/> grants
/// for the involved brick, accumulates it into the player's transient <see cref="SkillStateComponent"/>
/// (seeded from the client's statistics at join), and pushes the authoritative per-skill totals to the
/// acting player's client. Level-ups are detected here; surfacing them (as notifications) happens in
/// <see cref="OnInteractionApplied"/>. The server persists no skill data.
/// </summary>
public sealed class ServerSkillSystem
{
    private readonly SkillDatabase _skillDatabase;
    private readonly SessionManager _sessions;
    private readonly ServerConnectionHub _hub;
    private readonly ILogger<ServerSkillSystem> _logger;

    public ServerSkillSystem(
        in SkillDatabase skillDatabase,
        in SessionManager sessions,
        in ServerConnectionHub hub,
        in ILogger<ServerSkillSystem> logger
    ) {
        _skillDatabase = skillDatabase;
        _sessions = sessions;
        _hub = hub;
        _logger = logger;
    }

    /// <summary>
    /// Grants XP for a successfully applied interaction. <paramref name="isBreak"/> maps to
    /// <see cref="XPSource.Break"/>; otherwise the interaction is a place. The state component must exist
    /// on the player mirror (seeded at join); without a bound client the grant is skipped.
    /// </summary>
    public void OnInteractionApplied(DataStore store, int player, ushort brickDataID, bool isBreak)
    {
        if (!store.TryGet(player, out SkillStateComponent state) ||
            !_sessions.TryGetClient(player, out Uuid clientId))
        {
            return;
        }

        XPSource source = isBreak ? XPSource.Break : XPSource.Place;
        Result<SkillData[]> skills = _skillDatabase.Get(source);
        if (!skills.Success)
        {
            return;
        }

        bool mutated = false;
        foreach (SkillData skill in skills.Value)
        {
            if (!skill.TryGetXP(source, brickDataID, out int gainedXP))
            {
                continue;
            }

            long previousXP = state.GetXP(skill.ID);
            long totalXP = previousXP + gainedXP;
            state.SetXP(skill.ID, totalXP);
            mutated = true;

            LevelInfo previousLevel = skill.CalculateLevel(previousXP);
            LevelInfo currentLevel = skill.CalculateLevel(totalXP);

            _hub.Send(clientId, new SkillStateUpdateMessage
            {
                SkillId = skill.ID,
                TotalXP = totalXP,
                Level = currentLevel.Level,
                XPIntoLevel = currentLevel.XP,
                GainedXP = gainedXP,
            });

            SendXpBar(clientId, skill, currentLevel);

            if (previousLevel.Level != currentLevel.Level)
            {
                SendLevelUp(clientId, skill, previousLevel.Level, currentLevel.Level);
                _logger.LogDebug("Skill \"{skill}\" for player {player} leveled from {previous} to {level}.", skill.ID, player, previousLevel.Level, currentLevel.Level);
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
    private void SendXpBar(Uuid clientId, SkillData skill, LevelInfo currentLevel)
    {
        int nextLevelXP = skill.Levels.TryGetValue(currentLevel.Level + 1, out int next) ? next : 1;

        _hub.Send(clientId, new NotificationMessage
        {
            Type = (byte)NotificationType.Bar,
            Key = "notification.skill.bar",
            Args = [skill.Name, currentLevel.Level.ToString()],
            ID = skill.ID,
            Amount = (float)currentLevel.XP / nextLevelXP,
        });
    }

    private void SendLevelUp(Uuid clientId, SkillData skill, int previousLevel, int currentLevel)
    {
        _hub.Send(clientId, new NotificationMessage
        {
            Type = (byte)NotificationType.Toast,
            Key = "notification.skill.levelUp",
            Args = [skill.Name, previousLevel.ToString(), currentLevel.ToString()],
        });
    }
}