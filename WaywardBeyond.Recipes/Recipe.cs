using System;
using System.Collections.Generic;

namespace WaywardBeyond.Recipes;

/// <summary>A validated crafting or processing recipe.</summary>
public readonly struct Recipe
{
    private readonly RecipeIngredient[] _inputs;
    private readonly IReadOnlyList<RecipeIngredient> _inputsReadOnly;
    private readonly RecipeOutput[] _outputs;
    private readonly IReadOnlyList<RecipeOutput> _outputsReadOnly;

    internal Recipe(
        string id,
        string stationTag,
        RecipeMode mode,
        RecipeIngredient[] inputs,
        RecipeOutput[] outputs,
        float durationSeconds,
        float powerKw,
        string category,
        int order
    )
    {
        ID = id;
        StationTag = stationTag;
        Mode = mode;
        _inputs = inputs;
        _inputsReadOnly = Array.AsReadOnly(inputs);
        _outputs = outputs;
        _outputsReadOnly = Array.AsReadOnly(outputs);
        DurationSeconds = durationSeconds;
        PowerKw = powerKw;
        Category = category;
        Order = order;
    }

    /// <summary>The unique identifier of the recipe.</summary>
    public string ID { get; }

    /// <summary>The invariant station tag identifying stations that can execute this recipe.</summary>
    public string StationTag { get; }

    /// <summary>Whether the recipe triggers automatically or requires explicit selection.</summary>
    public RecipeMode Mode { get; }

    /// <summary>The required input ingredients.</summary>
    public IReadOnlyList<RecipeIngredient> Inputs => _inputsReadOnly ?? (IReadOnlyList<RecipeIngredient>)Array.Empty<RecipeIngredient>();

    /// <summary>The produced output stacks.</summary>
    public IReadOnlyList<RecipeOutput> Outputs => _outputsReadOnly ?? (IReadOnlyList<RecipeOutput>)Array.Empty<RecipeOutput>();

    /// <summary>A read-only span over the required input ingredients.</summary>
    public ReadOnlySpan<RecipeIngredient> InputSpan => _inputs;

    /// <summary>A read-only span over the produced output stacks.</summary>
    public ReadOnlySpan<RecipeOutput> OutputSpan => _outputs;

    /// <summary>The processing duration in seconds.</summary>
    public float DurationSeconds { get; }

    /// <summary>The power consumption (negative) or generation (positive) in kilowatts.</summary>
    public float PowerKw { get; }

    /// <summary>The optional category localization or grouping key.</summary>
    public string Category { get; }

    /// <summary>The deterministic sort order within a station recipe list.</summary>
    public int Order { get; }
}
