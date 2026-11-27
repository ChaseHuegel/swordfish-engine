using DryIoc;
using Shoal.DependencyInjection;
using Shoal.Extensions.Swordfish;
using Swordfish.Library.Collections;
using WaywardBeyond.Data.Localization;

namespace WaywardBeyond.Recipes;

/// <summary>Module injector for Wayward Beyond recipe assets.</summary>
// ReSharper disable once UnusedType.Global
public sealed class Injector : IDryIocInjector
{
    /// <inheritdoc/>
    public void Inject(IContainer container)
    {
        container.RegisterTags();
        container.RegisterTomlParser<RecipeDefinitions>();
        container.Register<RecipeDatabase>(Reuse.Singleton);
        container.RegisterMapping<IAutoActivate, RecipeDatabase>();
        container.RegisterMapping<IAssetDatabase<Recipe>, RecipeDatabase>();
        container.RegisterMapping<IRecipeDatabase, RecipeDatabase>();
    }
}
