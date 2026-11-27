using System;
using System.Collections.Generic;

namespace WaywardBeyond.Gameplay.Statistics;

/// <inheritdoc/>
public sealed class StatResolver : IStatResolver
{
    private const int PRIORITY_COUNT = 3;

    private readonly IStatModifierSource[] _sources;
    private readonly IReadOnlyList<IStatModifierSource> _readOnlySources;

    /// <summary>Builds an ordered resolver from every registered modifier source.</summary>
    public StatResolver(IEnumerable<IStatModifierSource> sources)
    {
        if (sources == null)
        {
            throw new ArgumentNullException(nameof(sources));
        }

        var buckets = new List<IStatModifierSource>[PRIORITY_COUNT];
        for (var i = 0; i < PRIORITY_COUNT; i++)
        {
            buckets[i] = [];
        }

        foreach (IStatModifierSource source in sources)
        {
            if (source == null)
            {
                throw new ArgumentException("A stat modifier source must not be null.", nameof(sources));
            }

            if (!Enum.IsDefined(source.Priority))
            {
                throw new ArgumentOutOfRangeException(nameof(sources), source.Priority, "The stat modifier priority is not defined.");
            }

            buckets[(int)source.Priority].Add(source);
        }

        var ordered = new List<IStatModifierSource>();
        for (var p = 0; p < PRIORITY_COUNT; p++)
        {
            ordered.AddRange(buckets[p]);
        }

        _sources = ordered.ToArray();
        _readOnlySources = Array.AsReadOnly(_sources);
    }

    /// <inheritdoc/>
    public float Evaluate(in StatQuery query)
    {
        var accumulator = new StatAccumulator(query.DefaultBase);
        if (string.IsNullOrEmpty(query.StatId))
        {
            return accumulator.Resolve();
        }

        for (var i = 0; i < _sources.Length; i++)
        {
            IStatModifierSource source = _sources[i];
            if (source.StatId != null && !string.Equals(source.StatId, query.StatId, StringComparison.Ordinal))
            {
                continue;
            }

            source.Apply(in query, ref accumulator);
        }

        return accumulator.Resolve();
    }

    /// <inheritdoc/>
    public IReadOnlyList<IStatModifierSource> GetSources() => _readOnlySources;
}
