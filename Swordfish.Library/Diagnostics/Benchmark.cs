using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using Swordfish.Library.Types;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Diagnostics;

public class Benchmark : IDisposable
{
    public static ConcurrentDictionary<string, ConcurrentBag<Benchmark>> History { get; } = new();

    public static Benchmark StartNew(string name) => new(name);

    public static Benchmark StartNew(params string[] trace) => new(string.Join(".", trace));

    public static List<string> CollectOutput()
    {
        var entries = new List<string>();
#if DEBUG
        foreach (KeyValuePair<string, ConcurrentBag<Benchmark>> pair in History)
        {
            var count = 0;
            var totalTime = new TimeSpan();
            var totalMemory = new ByteSize();
            while (pair.Value.TryTake(out Benchmark marker))
            {
                totalTime += marker.Timing;
                totalMemory += marker.Memory;
                count++;
            }

            entries.Add($"{pair.Key} count: {count} time: {totalTime.TotalMilliseconds} ms gc: {totalMemory}");
        }
        entries.Sort();
#endif
        return entries;
    }

    public TimeSpan Timing => _stopwatch.Elapsed;
    public ByteSize Memory { get; private set; }
    public string Name { get; }

    private bool _disposed;
    private readonly Stopwatch _stopwatch;
    private readonly long _gcStart;

    public Benchmark(string name)
    {
        Name = name;
#if DEBUG
        _gcStart = GC.GetTotalMemory(true);
        _stopwatch = Stopwatch.StartNew();
#else
            //  This is to ignore compiler warnings for unused/unassigned values in release
            _gcStart = 0;
            _stopwatch = null;
            _disposed = _stopwatch == null || _gcStart == 0 || _disposed;
#endif
    }

    public void Dispose()
    {
#if DEBUG
        _stopwatch.Stop();
        Memory = ByteSize.FromBytes(GC.GetTotalMemory(false) - _gcStart);

        if (_disposed)
        {
            return;
        }

        _disposed = true;

        ConcurrentBag<Benchmark> bag = History.GetOrAdd(Name, []);
        bag.Add(this);
#endif
    }

    public override bool Equals(object obj)
    {
        if (obj is Benchmark marker)
        {
            return marker.Name.Equals(Name);
        }

        return false;
    }

    public override int GetHashCode()
    {
        return Name.GetHashCode();
    }

    public override string ToString()
    {
        return $"{Name} time: {_stopwatch?.Elapsed.TotalMilliseconds} ms gc: {Memory}";
    }
}