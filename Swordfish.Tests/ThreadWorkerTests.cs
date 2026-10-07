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
        Thread.Sleep(500);
        worker.Stop();
        worker.Join();

        //  ~100 tps over 500 ms is 50 ticks; wide bounds absorb CI scheduling jitter.
        Assert.InRange(ticks, 25, 75);
    }

    [Fact]
    public void ReportsTickIntervalAsDelta()
    {
        float lastDelta = 0f;
        var worker = new ThreadWorker(delta => lastDelta = delta, "ThreadWorkerDeltaTest")
        {
            TargetTickRate = 100,
        };

        worker.Start();
        Thread.Sleep(300);
        worker.Stop();
        worker.Join();

        //  ~10 ms per tick at 100 tps.
        Assert.InRange(lastDelta, 0.004f, 0.03f);
    }
}
