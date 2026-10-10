using WaywardBeyond.Client.Systems;
using WaywardBeyond.Config;
using WaywardBeyond.Networking;

namespace WaywardBeyond.Client.Tests;

[TestFixture]
public class ChatServiceTests
{
    [Test]
    public void BoundsHistoryToCapacity()
    {
        var settings = new ChatConfig();
        settings.MaxHistory.Set(2);
        var service = new ChatService(settings);

        service.Add(new ChatMessage { Value = "1" });
        service.Add(new ChatMessage { Value = "2" });
        service.Add(new ChatMessage { Value = "3" });

        ChatLine[] snapshot = service.Snapshot();
        Assert.That(snapshot, Has.Length.EqualTo(2));
        Assert.That(snapshot[0].Message.Value, Is.EqualTo("2"));
        Assert.That(snapshot[1].Message.Value, Is.EqualTo("3"));
    }

    [Test]
    public void StampsNonDecreasingArrivalTimes()
    {
        var service = new ChatService(new ChatConfig());

        service.Add(new ChatMessage { Value = "first" });
        service.Add(new ChatMessage { Value = "second" });

        ChatLine[] snapshot = service.Snapshot();
        Assert.That(snapshot[0].ReceivedAt, Is.GreaterThan(0));
        Assert.That(snapshot[1].ReceivedAt, Is.GreaterThanOrEqualTo(snapshot[0].ReceivedAt));
    }

    [Test]
    public void SnapshotPreservesChronologicalOrder()
    {
        var service = new ChatService(new ChatConfig());

        service.Add(new ChatMessage { Value = "first" });
        service.Add(new ChatMessage { Value = "second" });

        ChatLine[] snapshot = service.Snapshot();
        Assert.That(snapshot.Select(line => line.Message.Value), Is.EqualTo(new[] { "first", "second" }));
    }

    [Test]
    public void SendQueueFlushesInOrder()
    {
        var service = new ChatService(new ChatConfig());

        service.EnqueueSend("a");
        service.EnqueueSend("b");

        Assert.That(service.TryDequeueSend(out string first), Is.True);
        Assert.That(first, Is.EqualTo("a"));
        Assert.That(service.TryDequeueSend(out string second), Is.True);
        Assert.That(second, Is.EqualTo("b"));
        Assert.That(service.TryDequeueSend(out string none), Is.False);
    }
}