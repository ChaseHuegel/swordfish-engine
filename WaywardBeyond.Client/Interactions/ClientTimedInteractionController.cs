using System;
using System.Collections.Generic;
using Swordfish.Library.IO;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay.Interactions;

namespace WaywardBeyond.Client.Interactions;

/// <summary>
/// Generic client-side timed hold state machine. Evaluates registered <see cref="IClientTimedInteraction"/>
/// providers, tracks local normalized progress, and emits single start/cancel interaction events without
/// hold-to-repeat chatter.
/// </summary>
internal sealed class ClientTimedInteractionController
{
    private readonly IClientTimedInteraction[] _interactions;

    private ActiveHoldSession _activeSession;
    private bool _hasActiveSession;

    /// <summary>Constructs the controller from all registered client timed interactions.</summary>
    public ClientTimedInteractionController(IEnumerable<IClientTimedInteraction> interactions)
    {
        if (interactions == null)
        {
            throw new ArgumentNullException(nameof(interactions));
        }

        var list = new List<IClientTimedInteraction>();
        foreach (IClientTimedInteraction interaction in interactions)
        {
            if (interaction != null)
            {
                list.Add(interaction);
            }
        }

        _interactions = list.ToArray();
    }

    /// <summary>Finds the first registered timed interaction claiming <paramref name="button"/> in the current context.</summary>
    public IClientTimedInteraction? FindActive(MouseButton button, in ClientTimedInteractionContext context)
    {
        for (var i = 0; i < _interactions.Length; i++)
        {
            IClientTimedInteraction candidate = _interactions[i];
            if (candidate.Button == button && candidate.AppliesTo(in context))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>Advances the active timed interaction for <paramref name="interaction"/> and returns normalized progress in <c>[0..1]</c>.</summary>
    public float Update(
        IClientTimedInteraction interaction,
        bool isButtonHeld,
        float delta,
        in ClientTimedInteractionContext context,
        out ClientTimedStageEvent stageEvent
    ) {
        stageEvent = ClientTimedStageEvent.None;
        if (!isButtonHeld)
        {
            Cancel(out stageEvent);
            return 0f;
        }

        if (!interaction.TryResolveTarget(in context, out ClientTimedTarget target))
        {
            Cancel(out stageEvent);
            return 0f;
        }

        if (!_hasActiveSession ||
            !MatchesActiveSession(in _activeSession, interaction, in target.Key, context.ActiveSlot, context.HeldItemId))
        {
            _hasActiveSession = true;
            _activeSession = new ActiveHoldSession
            {
                Interaction = interaction,
                Key = target.Key,
                ActiveSlot = context.ActiveSlot,
                HeldItemId = context.HeldItemId,
                CancelKind = target.CancelKind,
                Elapsed = 0f,
                RequiredDuration = target.RequiredDuration,
                InteractionSent = true,
                Completed = false,
            };
            stageEvent = new ClientTimedStageEvent(true, target.StartKind, target.BrickHint, target.PaintHint);
        }

        if (_activeSession.Completed)
        {
            return 1f;
        }

        float validDelta = float.IsFinite(delta) && delta > 0f ? delta : 0f;
        _activeSession.Elapsed += validDelta;
        _activeSession.RequiredDuration = target.RequiredDuration;
        float progress = TimedInteractionProgress.Normalize(_activeSession.Elapsed, target.RequiredDuration);
        _activeSession.Completed = TimedInteractionProgress.IsComplete(_activeSession.Elapsed, target.RequiredDuration);
        return progress;
    }

    /// <summary>Cancels any active timed hold session and outputs its cancel event if one was started.</summary>
    public void Cancel(out ClientTimedStageEvent stageEvent)
    {
        if (!_hasActiveSession)
        {
            stageEvent = ClientTimedStageEvent.None;
            return;
        }

        stageEvent = _activeSession.InteractionSent
            ? new ClientTimedStageEvent(true, _activeSession.CancelKind)
            : ClientTimedStageEvent.None;

        _hasActiveSession = false;
        _activeSession = default;
    }

    private static bool MatchesActiveSession(
        in ActiveHoldSession session,
        IClientTimedInteraction interaction,
        in TimedInteractionKey key,
        int activeSlot,
        string? heldItemId
    ) {
        return ReferenceEquals(session.Interaction, interaction) &&
            session.Key == key &&
            session.ActiveSlot == activeSlot &&
            string.Equals(session.HeldItemId, heldItemId, StringComparison.Ordinal);
    }

    private struct ActiveHoldSession
    {
        public IClientTimedInteraction Interaction;
        public TimedInteractionKey Key;
        public int ActiveSlot;
        public string? HeldItemId;
        public InteractionKind CancelKind;
        public float Elapsed;
        public float RequiredDuration;
        public bool InteractionSent;
        public bool Completed;
    }
}
