using DryIoc;
using Shoal.DependencyInjection;
using Shoal.Extensions.Swordfish;
using WaywardBeyond.Data.Localization;

namespace WaywardBeyond.Skills;

/// <summary>Module injector for Wayward Beyond skill assets.</summary>
// ReSharper disable once UnusedType.Global
public sealed class Injector : IDryIocInjector
{
    public void Inject(IContainer container)
    {
        container.RegisterTomlParser<SkillDefinitions>();
        container.RegisterTomlParser<LocalizedTagsDefinition>();
        container.Register<SkillDatabase>(Reuse.Singleton);
        container.RegisterMapping<ISkillDatabase, SkillDatabase>();
    }
}
