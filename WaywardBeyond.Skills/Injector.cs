using DryIoc;
using Shoal.DependencyInjection;
using Shoal.Extensions.Swordfish;
using Swordfish.Library.Collections;

namespace WaywardBeyond.Skills;

/// <summary>
/// Shoal module entry point for the shared skill definitions. Registers the headless skill database so
/// the authoritative server can run skill mechanics and the client can read definitions for display.
/// </summary>
public sealed class Injector : IDryIocInjector
{
    public void Inject(IContainer container)
    {
        container.RegisterTomlParser<SkillDefinitions>();
        container.RegisterTomlParser<SkillTagDefinition>();
        container.Register<SkillDatabase>(Reuse.Singleton);
        container.RegisterMapping<IAssetDatabase<SkillData>, SkillDatabase>();
    }
}