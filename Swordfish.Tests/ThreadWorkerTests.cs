using System;
using System.Threading;
using Swordfish.Library.Threading;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// The worker paces on wall-clock time and reports the true tick interval as its delta.
/// </summary>
public class ThreadWorkerTests
{
    [Fact]
    public void HonorsTargetTickRate()
    {
        int ticks = 0;
        var worker = new ThreadWorker(_ => Interlocked.Increment(ref ticks), "ThreadWorkerPacingTest")
        {
            TargetTickRate = 100,
        };

        worker.Start();
        Thread.Sleep(250);
        worker.Stop();
        worker.Join();

        //  ~100 tps over 250 ms is 25 ticks; wide bounds absorb CI scheduling jitter.
        Assert.InRange(ticks, 10, 45);
    }

    [Fact]
    public void ReportsTickIntervalAsDelta()
    {
        const int sampleCount = 8;
        var samples = new float[sampleCount];
        int count = 0;
        using var done = new ManualResetEventSlim(false);

        var worker = new ThreadWorker(delta =>
        {
            int index = Interlocked.Increment(ref count) - 1;
            if (index == 0)
            {
                return;
            }

            int sampleIndex = index - 1;
            if (sampleIndex < sampleCount)
            {
                samples[sampleIndex] = delta;
                if (sampleIndex == sampleCount - 1)
                {
                    done.Set();
                }
            }
        }, "ThreadWorkerDeltaTest")
        {
            TargetTickRate = 100,
        };

        worker.Start();
        bool completed = done.Wait(TimeSpan.FromSeconds(2));
        worker.Stop();
        worker.Join();

        Assert.True(completed, "Timed out waiting for ThreadWorker tick samples.");
        Array.Sort(samples);
        float medianDelta = samples[sampleCount / 2];

        //  ~10 ms per tick at 100 tps; median filters single-tick OS scheduling spikes.
        Assert.InRange(medianDelta, 0.004f, 0.03f);
    }
}
