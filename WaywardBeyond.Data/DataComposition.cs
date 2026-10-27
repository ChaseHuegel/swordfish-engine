using DryIoc;
using WaywardBeyond.Data.Localization;
using WaywardBeyond.Data.Migrations;

namespace WaywardBeyond.Data;

/// <summary>
/// The data project's composition entry. Every embedding that reads or writes save data calls
/// <see cref="RegisterDataServices"/>, so the shared data services cannot drift between compositions.
/// </summary>
public static class DataComposition
{
    /// <summary>
    ///     Registers the shared data services. First registration wins, so callers can compose in any
    ///     order without duplicating services.
    /// </summary>
    public static void RegisterDataServices(this IContainer container)
    {
        container.RegisterTags();
        container.Register<Migrator>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.Keep);
        container.Register<SeedPhraseService>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.Keep);
    }
}
