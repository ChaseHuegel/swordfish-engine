using System;
using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Shoal.DependencyInjection;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Localization;
using WaywardBeyond.Items;

namespace WaywardBeyond.Recipes;

/// <inheritdoc/>
internal sealed class RecipeDatabase : VirtualAssetDatabase<RecipeDefinitions, RecipeDefinition, Recipe>, IAutoActivate, IRecipeDatabase
{
    private const string TAG_PREFIX = "tag:";
    private const int STACK_MATCH_LIMIT = 128;

    private readonly IAssetDatabase<Item> _itemDatabase;
    private readonly LocalizedTags? _invariantTags;
    private readonly List<Recipe> _loaded = [];
    private readonly IReadOnlyList<Recipe> _all;
    private readonly Dictionary<string, IReadOnlyList<Recipe>> _byTargetTag = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Recipe[]> _autoByTargetTag = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _validInputsByTargetTag = new(StringComparer.Ordinal);

    /// <summary>Constructs and loads the recipe database.</summary>
    public RecipeDatabase(
        in ILogger<RecipeDatabase> logger,
        in IFileParseService fileParseService,
        in VirtualFileSystem vfs,
        in IAssetDatabase<Item> itemDatabase,
        in ILocalizedTagsDatabase tagDatabase
    ) : base(logger, fileParseService, vfs)
    {
        _itemDatabase = itemDatabase ?? throw new ArgumentNullException(nameof(itemDatabase));
        if (tagDatabase == null)
        {
            throw new ArgumentNullException(nameof(tagDatabase));
        }

        _invariantTags = tagDatabase.Invariant;
        Load();

        _loaded.Sort(CompareRecipes);
        _all = Array.AsReadOnly(_loaded.ToArray());
        BuildIndexes();
    }

    /// <inheritdoc/>
    public IReadOnlyList<Recipe> All => _all;

    /// <inheritdoc/>
    public IReadOnlyList<Recipe> GetByTargetTag(string targetTag)
    {
        if (string.IsNullOrWhiteSpace(targetTag))
        {
            return [];
        }

        return _byTargetTag.TryGetValue(targetTag, out IReadOnlyList<Recipe>? recipes) ? recipes : [];
    }

    /// <inheritdoc/>
    public Result<Recipe> FindAutoMatch(string targetTag, in InventoryComponent inputs)
    {
        return FindAutoMatch(targetTag, inputs.AsSpan());
    }

    /// <inheritdoc/>
    public Result<Recipe> FindAutoMatch(string targetTag, ReadOnlySpan<ItemData> inputs)
    {
        if (string.IsNullOrWhiteSpace(targetTag) || inputs.IsEmpty)
        {
            return Result<Recipe>.FromFailure("No matching automatic recipe found");
        }

        if (!_autoByTargetTag.TryGetValue(targetTag, out Recipe[]? candidates) || !HasAnyInput(inputs))
        {
            return Result<Recipe>.FromFailure("No matching automatic recipe found");
        }

        for (var i = 0; i < candidates.Length; i++)
        {
            if (MatchesInputs(in candidates[i], inputs))
            {
                return Result<Recipe>.FromSuccess(candidates[i]);
            }
        }

        return Result<Recipe>.FromFailure("No matching automatic recipe found");
    }

    /// <inheritdoc/>
    public bool IsValidInputForStation(string targetTag, string itemId)
    {
        if (string.IsNullOrWhiteSpace(targetTag) || string.IsNullOrEmpty(itemId))
        {
            return false;
        }

        return _validInputsByTargetTag.TryGetValue(targetTag, out HashSet<string>? validItems) &&
               validItems.Contains(itemId);
    }

    /// <inheritdoc/>
    protected override bool IsValidFile(PathInfo path) => path.HasExtension(".toml");

    /// <inheritdoc/>
    protected override PathInfo GetRootPath() => new("recipes/");

    /// <inheritdoc/>
    protected override IEnumerable<RecipeDefinition> GetAssetInfo(PathInfo path, RecipeDefinitions resource)
    {
        if (resource.Recipes != null)
        {
            for (var i = 0; i < resource.Recipes.Count; i++)
            {
                yield return resource.Recipes[i];
            }
        }
    }

    /// <inheritdoc/>
    protected override string GetAssetID(RecipeDefinition assetInfo) => assetInfo.ID?.Trim() ?? string.Empty;

    /// <inheritdoc/>
    protected override Result<Recipe> LoadAsset(string id, RecipeDefinition assetInfo)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return Result<Recipe>.FromFailure("Recipe is missing its ID");
        }

        if (Get(id).Success)
        {
            return Result<Recipe>.FromFailure($"Recipe ID \"{id}\" is declared more than once");
        }

        if (string.IsNullOrWhiteSpace(assetInfo.TargetTag))
        {
            return Result<Recipe>.FromFailure($"Recipe \"{id}\" is missing its TargetTag");
        }

        string targetTag = assetInfo.TargetTag.Trim();

        Result<RecipeMode> modeResult = ParseMode(id, assetInfo.Mode);
        if (!modeResult.Success)
        {
            return Result<Recipe>.FromFailure(modeResult.Message);
        }

        if (!float.IsFinite(assetInfo.DurationSeconds) || assetInfo.DurationSeconds < 0f)
        {
            return Result<Recipe>.FromFailure($"Recipe \"{id}\" has invalid DurationSeconds {assetInfo.DurationSeconds}");
        }

        if (!float.IsFinite(assetInfo.PowerKw))
        {
            return Result<Recipe>.FromFailure($"Recipe \"{id}\" has non-finite PowerKw {assetInfo.PowerKw}");
        }

        Result<RecipeIngredient[]> inputsResult = ResolveInputs(id, assetInfo.Inputs);
        if (!inputsResult.Success)
        {
            return Result<Recipe>.FromFailure(inputsResult.Message);
        }

        Result<RecipeOutput[]> outputsResult = ResolveOutputs(id, assetInfo.Outputs, assetInfo.PowerKw);
        if (!outputsResult.Success)
        {
            return Result<Recipe>.FromFailure(outputsResult.Message);
        }

        string category = string.IsNullOrWhiteSpace(assetInfo.Category) ? string.Empty : assetInfo.Category.Trim();
        var recipe = new Recipe(
            id,
            targetTag,
            modeResult.Value,
            inputsResult.Value,
            outputsResult.Value,
            assetInfo.DurationSeconds,
            assetInfo.PowerKw,
            category,
            assetInfo.Priority
        );

        _loaded.Add(recipe);
        return Result<Recipe>.FromSuccess(recipe);
    }

    private static Result<RecipeMode> ParseMode(string recipeId, string? rawMode)
    {
        if (string.IsNullOrWhiteSpace(rawMode))
        {
            return Result<RecipeMode>.FromSuccess(RecipeMode.Manual);
        }

        string trimmed = rawMode.Trim();
        char first = trimmed[0];
        if (char.IsDigit(first) || first == '-' || first == '+' || trimmed.IndexOf(',') >= 0)
        {
            return Result<RecipeMode>.FromFailure($"Recipe \"{recipeId}\" has invalid mode \"{rawMode}\"");
        }

        if (!Enum.TryParse(trimmed, ignoreCase: true, out RecipeMode parsed) || !Enum.IsDefined(parsed))
        {
            return Result<RecipeMode>.FromFailure($"Recipe \"{recipeId}\" has unknown mode \"{rawMode}\"");
        }

        return Result<RecipeMode>.FromSuccess(parsed);
    }

    private Result<RecipeIngredient[]> ResolveInputs(string recipeId, List<RecipeIngredientDefinition>? definitions)
    {
        if (definitions == null || definitions.Count == 0)
        {
            return Result<RecipeIngredient[]>.FromFailure($"Recipe \"{recipeId}\" must declare at least one input");
        }

        var ingredients = new RecipeIngredient[definitions.Count];
        for (var i = 0; i < definitions.Count; i++)
        {
            RecipeIngredientDefinition def = definitions[i];
            if (def.Count < 1)
            {
                return Result<RecipeIngredient[]>.FromFailure($"Recipe \"{recipeId}\" input #{i} must have count >= 1");
            }

            if (string.IsNullOrWhiteSpace(def.ItemId))
            {
                return Result<RecipeIngredient[]>.FromFailure($"Recipe \"{recipeId}\" input #{i} is missing item_id");
            }

            string spec = def.ItemId.Trim();
            string? slotGroup = string.IsNullOrWhiteSpace(def.SlotGroup) ? null : def.SlotGroup.Trim();

            if (spec.StartsWith(TAG_PREFIX, StringComparison.Ordinal))
            {
                string tag = spec[TAG_PREFIX.Length..].Trim();
                if (tag.Length == 0)
                {
                    return Result<RecipeIngredient[]>.FromFailure($"Recipe \"{recipeId}\" input #{i} has an empty tag reference");
                }

                IReadOnlyList<string>? tagItems = _invariantTags?.GetValues(tag);
                if (tagItems == null || tagItems.Count == 0)
                {
                    return Result<RecipeIngredient[]>.FromFailure($"Recipe \"{recipeId}\" input #{i} references unknown or empty invariant tag \"{tag}\"");
                }

                var uniqueItems = new List<string>(tagItems.Count);
                var seen = new HashSet<string>(StringComparer.Ordinal);
                for (var t = 0; t < tagItems.Count; t++)
                {
                    string memberId = tagItems[t];
                    if (!_itemDatabase.Get(memberId).Success)
                    {
                        return Result<RecipeIngredient[]>.FromFailure(
                            $"Recipe \"{recipeId}\" input #{i} tag \"{tag}\" references unknown item \"{memberId}\"");
                    }

                    if (seen.Add(memberId))
                    {
                        uniqueItems.Add(memberId);
                    }
                }

                string[] resolved = [.. uniqueItems];
                FrozenSet<string> resolvedSet = resolved.ToFrozenSet(StringComparer.Ordinal);
                ingredients[i] = new RecipeIngredient(spec, itemId: null, tag, def.Count, slotGroup, resolved, resolvedSet);
            }
            else
            {
                if (!_itemDatabase.Get(spec).Success)
                {
                    return Result<RecipeIngredient[]>.FromFailure($"Recipe \"{recipeId}\" input #{i} references unknown item \"{spec}\"");
                }

                ingredients[i] = new RecipeIngredient(spec, spec, tag: null, def.Count, slotGroup, [spec], resolvedItemSet: null);
            }
        }

        return Result<RecipeIngredient[]>.FromSuccess(ingredients);
    }

    private Result<RecipeOutput[]> ResolveOutputs(string recipeId, List<RecipeOutputDefinition>? definitions, float powerKw)
    {
        if (definitions == null || definitions.Count == 0)
        {
            if (powerKw > 0f)
            {
                return Result<RecipeOutput[]>.FromSuccess([]);
            }

            return Result<RecipeOutput[]>.FromFailure($"Recipe \"{recipeId}\" must declare at least one output or positive power_kw");
        }

        var outputs = new RecipeOutput[definitions.Count];
        for (var i = 0; i < definitions.Count; i++)
        {
            RecipeOutputDefinition def = definitions[i];
            if (string.IsNullOrWhiteSpace(def.ItemId))
            {
                return Result<RecipeOutput[]>.FromFailure($"Recipe \"{recipeId}\" output #{i} is missing item_id");
            }

            if (def.Count < 1)
            {
                return Result<RecipeOutput[]>.FromFailure($"Recipe \"{recipeId}\" output #{i} must have count >= 1");
            }

            float chance = def.Chance ?? 1f;
            if (!float.IsFinite(chance) || chance <= 0f || chance > 1f)
            {
                return Result<RecipeOutput[]>.FromFailure($"Recipe \"{recipeId}\" output #{i} chance must be in (0, 1], got {chance}");
            }

            string itemId = def.ItemId.Trim();
            if (!_itemDatabase.Get(itemId).Success)
            {
                return Result<RecipeOutput[]>.FromFailure($"Recipe \"{recipeId}\" output #{i} references unknown item \"{itemId}\"");
            }

            outputs[i] = new RecipeOutput(itemId, def.Count, chance);
        }

        return Result<RecipeOutput[]>.FromSuccess(outputs);
    }

    private void BuildIndexes()
    {
        var groupedAll = new Dictionary<string, List<Recipe>>(StringComparer.Ordinal);
        var groupedAuto = new Dictionary<string, List<Recipe>>(StringComparer.Ordinal);

        for (var i = 0; i < _loaded.Count; i++)
        {
            Recipe recipe = _loaded[i];

            if (!groupedAll.TryGetValue(recipe.TargetTag, out List<Recipe>? targetList))
            {
                targetList = [];
                groupedAll[recipe.TargetTag] = targetList;
            }

            targetList.Add(recipe);

            if (recipe.Mode == RecipeMode.Auto)
            {
                if (!groupedAuto.TryGetValue(recipe.TargetTag, out List<Recipe>? autoList))
                {
                    autoList = [];
                    groupedAuto[recipe.TargetTag] = autoList;
                }

                autoList.Add(recipe);
            }

            if (!_validInputsByTargetTag.TryGetValue(recipe.TargetTag, out HashSet<string>? validInputs))
            {
                validInputs = new HashSet<string>(StringComparer.Ordinal);
                _validInputsByTargetTag[recipe.TargetTag] = validInputs;
            }

            ReadOnlySpan<RecipeIngredient> ingredients = recipe.InputSpan;
            for (var k = 0; k < ingredients.Length; k++)
            {
                string[] resolved = ingredients[k].ResolvedItemArray;
                for (var r = 0; r < resolved.Length; r++)
                {
                    validInputs.Add(resolved[r]);
                }
            }
        }

        foreach ((string targetTag, List<Recipe> recipes) in groupedAll)
        {
            _byTargetTag[targetTag] = Array.AsReadOnly(recipes.ToArray());
        }

        foreach ((string targetTag, List<Recipe> autoRecipes) in groupedAuto)
        {
            _autoByTargetTag[targetTag] = autoRecipes.ToArray();
        }
    }

    private static int CompareRecipes(Recipe left, Recipe right)
    {
        int priorityCompare = left.Priority.CompareTo(right.Priority);
        return priorityCompare != 0 ? priorityCompare : StringComparer.Ordinal.Compare(left.ID, right.ID);
    }

    private static bool HasAnyInput(ReadOnlySpan<ItemData> inputs)
    {
        for (var i = 0; i < inputs.Length; i++)
        {
            if (!inputs[i].IsEmpty())
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesInputs(in Recipe recipe, ReadOnlySpan<ItemData> inputs)
    {
        ReadOnlySpan<RecipeIngredient> ingredients = recipe.InputSpan;
        if (ingredients.Length == 0)
        {
            return false;
        }

        int[]? rented = null;
        Span<int> remaining = inputs.Length <= STACK_MATCH_LIMIT
            ? stackalloc int[inputs.Length]
            : (rented = ArrayPool<int>.Shared.Rent(inputs.Length)).AsSpan(0, inputs.Length);

        try
        {
            for (var i = 0; i < inputs.Length; i++)
            {
                remaining[i] = inputs[i].IsEmpty() ? 0 : inputs[i].Count;
            }

            //  Match exact item ingredients first so narrow requirements are not consumed by broader tag matches.
            if (!ConsumeIngredients(ingredients, inputs, remaining, matchTags: false))
            {
                return false;
            }

            return ConsumeIngredients(ingredients, inputs, remaining, matchTags: true);
        }
        finally
        {
            if (rented != null)
            {
                ArrayPool<int>.Shared.Return(rented);
            }
        }
    }

    private static bool ConsumeIngredients(
        ReadOnlySpan<RecipeIngredient> ingredients,
        ReadOnlySpan<ItemData> inputs,
        Span<int> remaining,
        bool matchTags
    )
    {
        for (var i = 0; i < ingredients.Length; i++)
        {
            ref readonly RecipeIngredient ingredient = ref ingredients[i];
            if (ingredient.IsTag != matchTags)
            {
                continue;
            }

            int needed = ingredient.Count;
            for (var s = 0; s < inputs.Length; s++)
            {
                if (remaining[s] <= 0)
                {
                    continue;
                }

                if (!ingredient.Matches(inputs[s].ID))
                {
                    continue;
                }

                int take = Math.Min(remaining[s], needed);
                remaining[s] -= take;
                needed -= take;
                if (needed == 0)
                {
                    break;
                }
            }

            if (needed > 0)
            {
                return false;
            }
        }

        return true;
    }
}
