using System;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Server.Components;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// The authoritative interaction pump. Runs once per server tick, between the replication apply stage
/// (which drains inbound client interaction events into each player mirror's staged buffer) and the
/// publish stage. For each staged interaction at or below the current sim tick it builds the running
/// context and hands it to the interaction pipeline, which decides the content, outcome, authority
/// change, and consequences. See the interaction-pipeline spec for the stage contract.
/// </summary>
public sealed class ServerInteractionSystem(
    SessionManager sessions,
    ILogger<ServerInteractionSystem> logger,
    IInteractionHandlerRegistry handlerRegistry,
    Func<DataStore, IVoxelInteractionWorld> worldFactory,
    SimulationStep simulationStep
) : IServerWorldSystem
{
    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        Tick(delta, store, simulationStep.CurrentSimTick);
    }

    /// <summary>Processes staged interactions for the given sim tick on the authoritative server world.</summary>
    public void Tick(float delta, DataStore store, uint simTick)
    {
        ConsumeAction action = new() { Owner = this, Store = store, SimTick = simTick };
        store.QueryRef<NetworkComponent, OwnedCharacterComponent, TransformComponent, ConsumeAction>(0f, ref action);
    }

    private void Consume(
        DataStore store,
        int entity,
        uint simTick,
        in NetworkComponent net,
        in TransformComponent transform,
        out uint consumedSequence
    ) {
        consumedSequence = net.LastConsumedInteractionSequence;

        if (net.StagedInteractions == null)
        {
            return;
        }

        StageRing<InteractionEvent> buffer = net.StagedInteractions;

        Result<InteractionEvent> staged = buffer.Consume(simTick, consumedSequence);
        while (staged.Success)
        {
            InteractionEvent interaction = staged.Value;
            consumedSequence = interaction.SequenceNumber;
            ProcessInteraction(store, entity, in interaction, in transform);
            staged = buffer.Consume(simTick, consumedSequence);
        }
    }

    private void ProcessInteraction(DataStore store, int player, in InteractionEvent interaction, in TransformComponent mirror)
    {
        var kind = (InteractionKind)interaction.Kind;

        string? heldItemID = GetHeldItemID(store, player);
        GameMode mode = store.TryGet(player, out GameModeComponent gameMode) ? gameMode.Mode : GameMode.Creative;
        IVoxelInteractionWorld world = worldFactory(store);
        Uuid clientId = sessions.TryGetClient(player, out Uuid client) ? client : Uuid.Null;

        //  The hint and the mirror transform are the only client-authored inputs; the pipeline resolves
        //  the rest against the authority store.
        var request = new InteractionRequest(
            mirror.Position,
            interaction.Brick,
            kind,
            null,
            mode,
            CharacterDefaults.DEFAULT_REACH
        );

        var context = new InteractionContext(store, player, clientId, interaction.SequenceNumber, world, in request, heldItemID);
        InteractionRunResult result = handlerRegistry.Run(ref context);

        if (result.Decision is InteractionDecision.Consume or InteractionDecision.Cancel)
        {
            logger.LogDebug(
                "Interaction {sequence} for player {player} ended with {decision}.",
                interaction.SequenceNumber, player, result.Decision);
            return;
        }

        if (result.Resolution.Action == InteractionAction.None)
        {
            logger.LogDebug(
                "Interaction {sequence} for player {player} resolved to nothing.",
                interaction.SequenceNumber, player);
        }
    }

    private static string? GetHeldItemID(DataStore store, int player)
    {
        if (!store.TryGet(player, out EquipmentComponent equipment) ||
            !store.TryGet(player, out InventoryComponent inventory))
        {
            return null;
        }

        int slot = equipment.ActiveInventorySlot;
        if (slot < 0 || slot >= inventory.Contents.Length)
        {
            return null;
        }

        return inventory.Contents[slot].ID;
    }

    private struct ConsumeAction : IForEachRef<NetworkComponent, OwnedCharacterComponent, TransformComponent>
    {
        public ServerInteractionSystem Owner;
        public DataStore Store;
        public uint SimTick;

        public void Execute(float delta, DataStore store, int entity, ref Ref<NetworkComponent> net, ref Ref<OwnedCharacterComponent> owned, ref Ref<TransformComponent> transform)
        {
            ref readonly NetworkComponent current = ref net.Read;
            ref readonly TransformComponent transformValue = ref transform.Read;

            Owner.Consume(Store, entity, SimTick, in current, in transformValue, out uint consumedSequence);

            //  Change-gated: a write of an unchanged value still marks the component dirty.
            if (consumedSequence != current.LastConsumedInteractionSequence)
            {
                net.Write.LastConsumedInteractionSequence = consumedSequence;
            }
        }
    }
}
