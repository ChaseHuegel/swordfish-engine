using System;
using System.Numerics;
using Reef;
using Swordfish.UI.Reef;
using Xunit;

namespace Swordfish.Tests;

public class ReefBatchTests
{
    private const int WIDTH = 100;
    private const int HEIGHT = 100;
    private const int STRIDE = ReefBatch.INSTANCE_STRIDE;

    private const float U0 = 0f;
    private const float V0 = 0f;
    private const float U1 = 1f;
    private const float V1 = 1f;

    private static readonly IntRect Clip = new(0, 0, WIDTH, HEIGHT);

    private static ReefBatch CreateBatch()
    {
        var batch = new ReefBatch();
        batch.Begin(WIDTH, HEIGHT);
        return batch;
    }

    [Fact]
    public void QuadsUnderOneStateShareOneRun()
    {
        ReefBatch batch = CreateBatch();
        batch.UseState(1);
        batch.AddQuad(new IntRect(0, 0, 10, 10), U0, V0, U1, V1, Vector4.One, Clip);
        batch.AddQuad(new IntRect(10, 10, 20, 20), U0, V0, U1, V1, Vector4.One, Clip);

        batch.End();

        Assert.Equal(2, batch.InstanceCount);
        Assert.Equal(1, batch.RunCount);

        ReefBatch.Run run = batch.GetRun(0);
        Assert.Equal(0, run.BaseInstance);
        Assert.Equal(2, run.InstanceCount);
        Assert.Equal(1, run.State);
    }

    [Fact]
    public void StateChangeSplitsRunsInPaintOrder()
    {
        ReefBatch batch = CreateBatch();
        batch.UseState(1);
        batch.AddQuad(new IntRect(0, 0, 10, 10), U0, V0, U1, V1, Vector4.One, Clip);
        batch.UseState(2);
        batch.AddQuad(new IntRect(10, 10, 20, 20), U0, V0, U1, V1, Vector4.One, Clip);
        batch.UseState(1);
        batch.AddQuad(new IntRect(20, 20, 30, 30), U0, V0, U1, V1, Vector4.One, Clip);

        batch.End();

        Assert.Equal(3, batch.RunCount);
        Assert.Equal(3, batch.InstanceCount);

        Assert.Equal(1, batch.GetRun(0).State);
        Assert.Equal(2, batch.GetRun(1).State);
        Assert.Equal(1, batch.GetRun(2).State);

        Assert.Equal(0, batch.GetRun(0).BaseInstance);
        Assert.Equal(1, batch.GetRun(1).BaseInstance);
        Assert.Equal(2, batch.GetRun(2).BaseInstance);

        for (var i = 0; i < batch.RunCount; i++)
        {
            Assert.Equal(1, batch.GetRun(i).InstanceCount);
        }
    }

    [Fact]
    public void RepeatedStateDoesNotSplitRuns()
    {
        ReefBatch batch = CreateBatch();
        batch.UseState(7);
        batch.AddQuad(new IntRect(0, 0, 10, 10), U0, V0, U1, V1, Vector4.One, Clip);
        batch.UseState(7);
        batch.AddQuad(new IntRect(10, 10, 20, 20), U0, V0, U1, V1, Vector4.One, Clip);

        batch.End();

        Assert.Equal(1, batch.RunCount);
        Assert.Equal(2, batch.GetRun(0).InstanceCount);
    }

    [Fact]
    public void EmptyRunsAreDropped()
    {
        ReefBatch batch = CreateBatch();
        batch.UseState(1);
        batch.UseState(2);
        batch.AddQuad(new IntRect(0, 0, 10, 10), U0, V0, U1, V1, Vector4.One, Clip);
        batch.UseState(3);

        batch.End();

        Assert.Equal(1, batch.RunCount);
        Assert.Equal(2, batch.GetRun(0).State);
    }

    [Fact]
    public void AStateThatDrawsNothingDoesNotSplitRuns()
    {
        ReefBatch batch = CreateBatch();
        batch.UseState(1);
        batch.AddQuad(new IntRect(0, 0, 10, 10), U0, V0, U1, V1, Vector4.One, Clip);
        batch.UseState(2);
        batch.UseState(1);
        batch.AddQuad(new IntRect(10, 10, 20, 20), U0, V0, U1, V1, Vector4.One, Clip);

        batch.End();

        Assert.Equal(1, batch.RunCount);
        Assert.Equal(2, batch.GetRun(0).InstanceCount);
    }

    [Fact]
    public void InvisibleQuadsAreSkipped()
    {
        ReefBatch batch = CreateBatch();
        batch.UseState(1);

        batch.AddQuad(new IntRect(0, 0, 0, 10), U0, V0, U1, V1, Vector4.One, Clip);
        batch.AddQuad(new IntRect(0, 0, 10, 0), U0, V0, U1, V1, Vector4.One, Clip);
        batch.AddQuad(new IntRect(0, 0, 10, 10), U0, V0, U1, V1, Vector4.Zero, Clip);
        batch.AddQuad(new IntRect(0, 0, 10, 10), U0, V0, U1, V1, new Vector4(1f, 1f, 1f, 0f), Clip);

        batch.End();

        Assert.Equal(0, batch.InstanceCount);
        Assert.Equal(0, batch.RunCount);
        Assert.True(batch.Instances.IsEmpty);
    }

    [Fact]
    public void QuadsWithoutAStateAreSkipped()
    {
        ReefBatch batch = CreateBatch();
        batch.AddQuad(new IntRect(0, 0, 10, 10), U0, V0, U1, V1, Vector4.One, Clip);

        batch.End();

        Assert.Equal(0, batch.InstanceCount);
        Assert.Equal(0, batch.RunCount);
    }

    [Fact]
    public void InstancesAreWrittenInPaintOrder()
    {
        ReefBatch batch = CreateBatch();
        batch.UseState(1);
        batch.AddQuad(new IntRect(0, 0, 10, 10), U0, V0, U1, V1, new Vector4(1f, 0f, 0f, 1f), Clip);
        batch.AddQuad(new IntRect(10, 10, 20, 20), U0, V0, U1, V1, new Vector4(0f, 1f, 0f, 1f), Clip);

        batch.End();

        ReadOnlySpan<float> instances = batch.Instances;

        Assert.Equal(2 * STRIDE, instances.Length);
        Assert.Equal(0, batch.GetRun(0).BaseInstance);
        Assert.Equal(2, batch.GetRun(0).InstanceCount);

        //  The first quad is red and the second is green, with nothing reordered.
        Assert.Equal(1f, instances[8]);
        Assert.Equal(0f, instances[9]);
        Assert.Equal(0f, instances[STRIDE + 8]);
        Assert.Equal(1f, instances[STRIDE + 9]);
    }

    [Fact]
    public void ScreenCornersMapToClipSpace()
    {
        ReefBatch batch = CreateBatch();
        batch.UseState(1);
        batch.AddQuad(new IntRect(0, 0, WIDTH, HEIGHT), U0, V0, U1, V1, Vector4.One, Clip);

        batch.End();

        ReadOnlySpan<float> instance = batch.Instances;

        //  Bottom left, bottom right, top right, top left, as the shader reads them.
        Assert.Equal(-1f, instance[0], 5);
        Assert.Equal(-1f, instance[1], 5);
        Assert.Equal(1f, instance[2], 5);
        Assert.Equal(1f, instance[3], 5);
    }

    [Fact]
    public void AnInsetRectKeepsItsCornersInOrder()
    {
        ReefBatch batch = CreateBatch();
        batch.UseState(1);
        batch.AddQuad(new IntRect(25, 25, 75, 75), U0, V0, U1, V1, Vector4.One, Clip);

        batch.End();

        ReadOnlySpan<float> instance = batch.Instances;

        float left = instance[0];
        float bottom = instance[1];
        float right = instance[2];
        float top = instance[3];

        Assert.Equal(-0.5f, left, 5);
        Assert.Equal(-0.5f, bottom, 5);
        Assert.Equal(0.5f, right, 5);
        Assert.Equal(0.5f, top, 5);
    }

    [Fact]
    public void ColorClipAndTextureCoordinatesAreWrittenPerQuad()
    {
        ReefBatch batch = CreateBatch();
        batch.UseState(1);
        var clip = new IntRect(1, 2, 3, 4);
        var color = new Vector4(0.1f, 0.2f, 0.3f, 0.4f);
        batch.AddQuad(new IntRect(0, 0, 10, 10), 0.25f, 0.5f, 0.75f, 1f, color, clip);

        batch.End();

        ReadOnlySpan<float> instance = batch.Instances;

        Assert.Equal(0.25f, instance[4]);
        Assert.Equal(0.5f, instance[5]);
        Assert.Equal(0.75f, instance[6]);
        Assert.Equal(1f, instance[7]);

        Assert.Equal(0.1f, instance[8], 5);
        Assert.Equal(0.2f, instance[9], 5);
        Assert.Equal(0.3f, instance[10], 5);
        Assert.Equal(0.4f, instance[11], 5);

        Assert.Equal(1f, instance[12]);
        Assert.Equal(2f, instance[13]);
        Assert.Equal(3f, instance[14]);
        Assert.Equal(4f, instance[15]);
    }

    [Fact]
    public void AQuadCostsOneInstanceOfSixteenFloats()
    {
        ReefBatch batch = CreateBatch();
        batch.UseState(1);
        batch.AddQuad(new IntRect(0, 0, 10, 10), U0, V0, U1, V1, Vector4.One, Clip);

        batch.End();

        //  Four vertices of fourteen floats would be fifty six.
        Assert.Equal(16, ReefBatch.INSTANCE_STRIDE);
        Assert.Equal(STRIDE, batch.Instances.Length);
    }

    [Fact]
    public void BeginResetsTheFrame()
    {
        ReefBatch batch = CreateBatch();
        batch.UseState(1);
        batch.AddQuad(new IntRect(0, 0, 10, 10), U0, V0, U1, V1, Vector4.One, Clip);

        batch.Begin(WIDTH, HEIGHT);

        Assert.Equal(0, batch.InstanceCount);
        Assert.Equal(0, batch.RunCount);
        Assert.True(batch.Instances.IsEmpty);
    }

    [Fact]
    public void GrowingPastTheInitialCapacityKeepsOrder()
    {
        ReefBatch batch = CreateBatch();
        batch.UseState(1);

        const int QUADS = 1_000;
        for (var i = 0; i < QUADS; i++)
        {
            batch.AddQuad(new IntRect(i, i, i + 1, i + 1), U0, V0, U1, V1, Vector4.One, Clip);
        }

        batch.End();

        Assert.Equal(QUADS, batch.InstanceCount);
        Assert.Equal(1, batch.RunCount);
        Assert.Equal(QUADS * STRIDE, batch.Instances.Length);
        Assert.Equal(0, batch.GetRun(0).BaseInstance);
        Assert.Equal(QUADS, batch.GetRun(0).InstanceCount);
    }
}
