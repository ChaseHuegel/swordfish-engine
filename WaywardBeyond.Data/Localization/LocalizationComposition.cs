using DryIoc;

namespace WaywardBeyond.Data.Localization;

/// <summary>
/// The localization area's composition entry. Any module that needs localized tags calls
/// <see cref="RegisterTags"/> instead of registering the database and parser itself.
/// </summary>
public static class LocalizationComposition
{
    /// <summary>
    ///     Registers the localized tag database and its parser. First registration wins, so any module
    ///     that needs tags can call this without duplicating services.
    /// </summary>
    public static void RegisterTags(this IContainer container)
    {
        container.RegisterMany<LocalizedTagsDatabase>(reuse: Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.Keep);
        container.RegisterMany<LocalizedTagDefinitionParser>(ifAlreadyRegistered: IfAlreadyRegistered.AppendNewImplementation);
    }
}