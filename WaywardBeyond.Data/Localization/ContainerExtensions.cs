using DryIoc;

namespace WaywardBeyond.Data.Localization;

public static class ContainerExtensions
{
    public static void RegisterTags(this IContainer container)
    {
        container.RegisterMany<LocalizedTagsDatabase>(reuse: Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.Keep);
        container.RegisterMany<LocalizedTagDefinitionParser>(reuse: Reuse.Transient, ifAlreadyRegistered: IfAlreadyRegistered.Keep);
    }
}