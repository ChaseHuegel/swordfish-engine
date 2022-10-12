using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using Swordfish.Library.Types;

namespace Swordfish.Library.Diagnostics
{
    public class Benchmark : IDisposable
    {
        public static ConcurrentDictionary<string, ConcurrentBag<Benchmark>> History { get; private set; }
            = new ConcurrentDictionary<string, ConcurrentBag<Benchmark>>();

        public static Benchmark StartNew(string name) => new Benchmark(name);

        public static Benchmark StartNew(params string[] trace) => new Benchmark(string.Join(".", trace));

        public static List<string> CollectOutput()
        {
            List<string> entries = new List<string>();
#if DEBUG
            foreach (var pair in History)
            {
                int count = 0;
                TimeSpan totalTime = new TimeSpan();
                ByteSize totalMemory = new ByteSize();
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

        public static void Log()
        {
            foreach (string entry in CollectOutput())
                Debugger.Log(entry);
        }

        public TimeSpan Timing => Stopwatch.Elapsed;
        public ByteSize Memory { get; private set; }
        public string Name { get; private set; }

        private bool Disposed;
        private readonly Stopwatch Stopwatch;
        private readonly long GCStart;

        public Benchmark(string name)
        {
            Name = name;
#if DEBUG
            GCStart = GC.GetTotalMemory(true);
            Stopwatch = Stopwatch.StartNew();
#else
            //  This is to ignore compiler warnings for unused/unassigned values in release
            GCStart = 0;
            Stopwatch = null;
            Disposed = Stopwatch == null || GCStart == 0 || Disposed;
#endif
        }

        public void Dispose()
        {
#if DEBUG
            Stopwatch.Stop();
            Memory = ByteSize.FromBytes(GC.GetTotalMemory(false) - GCStart);

            if (Disposed)
                return;

            Disposed = true;

            var bag = History.GetOrAdd(Name, new ConcurrentBag<Benchmark>());
            bag.Add(this);
#endif
        }

        public override bool Equals(object obj)
        {
            if (obj is Benchmark marker)
                return marker.Name.Equals(Name);

            return false;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public override string ToString()
        {
            return $"{Name} time: {Stopwatch?.Elapsed.TotalMilliseconds} ms gc: {Memory}";
        }
    }
}
