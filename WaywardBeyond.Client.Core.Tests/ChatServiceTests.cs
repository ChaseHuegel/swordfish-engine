using WaywardBeyond.Client.Core.Systems;
using WaywardBeyond.Shared.Config;
using WaywardBeyond.Shared.Networking;

namespace WaywardBeyond.Client.Core.Tests;

[TestFixture]
public class ChatServiceTests
{
    [Test]
    public void BoundsHistoryToCapacity()
    {
        var settings = new ChatSettings();
        settings.MaxHistory.Set(2);
        var service = new ChatService(settings);

        service.Add(new ChatMessage { Value = "1" });
        service.Add(new ChatMessage { Value = "2" });
        service.Add(new ChatMessage { Value = "3" });

        ChatMessage[] snapshot = service.Snapshot();
        Assert.That(snapshot, Has.Length.EqualTo(2));
        Assert.That(snapshot[0].Value, Is.EqualTo("2"));
        Assert.That(snapshot[1].Value, Is.EqualTo("3"));
    }

    [Test]
    public void SnapshotPreservesChronologicalOrder()
    {
        var service = new ChatService(new ChatSettings());

        service.Add(new ChatMessage { Value = "first" });
        service.Add(new ChatMessage { Value = "second" });

        ChatMessage[] snapshot = service.Snapshot();
        Assert.That(snapshot.Select(message => message.Value), Is.EqualTo(new[] { "first", "second" }));
    }

    [Test]
    public void SendQueueFlushesInOrder()
    {
        var service = new ChatService(new ChatSettings());

        service.EnqueueSend("a");
        service.EnqueueSend("b");

        Assert.That(service.TryDequeueSend(out string first), Is.True);
        Assert.That(first, Is.EqualTo("a"));
        Assert.That(service.TryDequeueSend(out string second), Is.True);
        Assert.That(second, Is.EqualTo("b"));
        Assert.That(service.TryDequeueSend(out string none), Is.False);
    }
}