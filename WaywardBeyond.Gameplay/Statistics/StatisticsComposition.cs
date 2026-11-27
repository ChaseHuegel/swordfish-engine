using System;
using DryIoc;

namespace WaywardBeyond.Gameplay.Statistics;

/// <summary>
/// Composition extensions for the statistics modifier pipeline. Embeddings call <see cref="RegisterStatistics"/>
/// to register the built-in sources and resolver once; modules and mods call
/// <see cref="RegisterStatModifierSource{T}"/> to contribute additional modifier sources.
/// </summary>
public static class StatisticsComposition
{
    /// <summary>
    /// Registers the built-in <see cref="IStatModifierSource"/> implementations and the shared
    /// <see cref="IStatResolver"/>. Safe to call from multiple compositions in any order.
    /// </summary>
    public static void RegisterStatistics(this IContainer container)
    {
        ArgumentNullException.ThrowIfNull(container);

        container.RegisterStatModifierSource<BrickStatisticModifierSource>();
        container.RegisterStatModifierSource<ItemStatisticModifierSource>();
        container.RegisterStatModifierSource<EntityStatisticModifierSource>();
        container.RegisterStatModifierSource<SkillMiningSpeedModifierSource>();
        container.Register<StatResolver>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.Keep);
        container.RegisterMapping<IStatResolver, StatResolver>(ifAlreadyRegistered: IfAlreadyRegistered.Keep);
    }

    /// <summary>
    /// Registers a singleton <see cref="IStatModifierSource"/> implementation idempotently so game modules
    /// and mods can contribute modifiers without duplicating sources across host/client compositions.
    /// </summary>
    public static void RegisterStatModifierSource<T>(this IContainer container)
        where T : class, IStatModifierSource
    {
        ArgumentNullException.ThrowIfNull(container);

        if (container.IsRegistered<T>())
        {
            return;
        }

        container.Register<T>(Reuse.Singleton);
        container.RegisterMapping<IStatModifierSource, T>(ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
    }
}
