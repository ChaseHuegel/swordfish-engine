
using System.Collections.Concurrent;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Threading;

namespace Swordfish.Networking.MessageProcessing;

public class PacketProcessor : IMessageProcessor<Packet>, IDisposable
{
    private readonly AutoResetEvent MessageSignal = new(false);

    private readonly ThreadWorker ThreadWorker;

    private ConcurrentQueue<Packet>? _messages = new();

    public event EventHandler<Packet>? Received;

    public PacketProcessor()
    {
        ThreadWorker = new ThreadWorker(ProcessQueue, nameof(PacketProcessor));
    }

    public void Dispose()
    {
        ThreadWorker.Stop();
        _messages = null;
        MessageSignal.Set();
    }

    public void Post(Packet message)
    {
        _messages?.Enqueue(message);
        MessageSignal.Set();
    }

    private void SafeInvokeReceived(Packet packet)
    {
        try {
            Received?.Invoke(this, packet);
        } catch {
            //  Swallow exceptions thrown by listeners.
        }
    }

    private void ProcessQueue()
    {
        do
        {
            while (_messages != null && _messages.TryDequeue(out Packet packet))
                SafeInvokeReceived(packet);
        } while (_messages != null && MessageSignal.WaitOne());
    }

    private void ProccessPacket(Packet packet)
    {
        // NetEventArgs netEventArgs = new NetEventArgs
        // {
        //     Packet = packet,
        //     EndPoint = endPoint
        // };

        // try
        // {
        //     PacketDefinition packetDefinition = PacketManager.GetPacketDefinition(packet.PacketID);
        //     SequencePair currentSequence = PacketSequences.GetOrAdd(packetDefinition.ID, SequencePairFactory);

        //     netEventArgs.PacketID = packet.PacketID;

        //     Stats.RecordPacketRecieved();
        //     Stats.RecordBytesIn(buffer.Length);
        //     PacketReceived?.Invoke(this, netEventArgs);

        //     //  The packet is accepted if:
        //     //  -   the packet doesn't require a session
        //     //  -   OR the provided session is valid
        //     //  -   AND the sequence is new IF the packet is ordered
        //     NetSession session = null;
        //     if ((!packetDefinition.RequiresSession || IsSessionValid(endPoint, packet.SessionID, out session))
        //         && (!packetDefinition.Ordered || packet.Sequence >= currentSequence.Received))
        //     {
        //         //  Any valid communication should refresh sessions.
        //         session?.RefreshExpiration();

        //         //  Update this packet sequence
        //         currentSequence.Received = packet.Sequence;

        //         //  Deserialize the data and invoke the packet's handlers
        //         IDataBody deserializeData = NeedlefishFormatter.Deserialize(packetDefinition.Type, buffer);

        //         //  Process reliable packets
        //         if (packetDefinition.Reliable)
        //         {
        //             //  If it is an ack, then one of our reliable packets has been received.
        //             if (packetDefinition.Type == typeof(AckPacket))
        //             {
        //                 AckPacket ack = (AckPacket)deserializeData;
        //                 UnregisterReliablePacket(endPoint, ack.AckPacketID, ack.AckSequence);
        //             }
        //             //  Else, we should ack this packet.
        //             else
        //             {
        //                 Send(AckPacket.New(packet.PacketID, packet.Sequence), endPoint);
        //             }
        //         }

        //         netEventArgs.Packet = (Packet)deserializeData;
        //         netEventArgs.Session = session;
        //         Stats.RecordPacketAccepted();
        //         Stats.RecordBytesAccepted(buffer.Length);
        //         PacketAccepted?.Invoke(this, netEventArgs);

        //         foreach (PacketHandler handler in packetDefinition.Handlers)
        //         {
        //             switch (handler.Type)
        //             {
        //                 case PacketHandlerType.SERVER:
        //                     if (this is NetServer) handler.Method.Invoke(null, new object[] { this, deserializeData, netEventArgs });
        //                     break;
        //                 case PacketHandlerType.CLIENT:
        //                     if (this is NetClient) handler.Method.Invoke(null, new object[] { this, deserializeData, netEventArgs });
        //                     break;
        //                 case PacketHandlerType.AGNOSTIC:
        //                     handler.Method.Invoke(null, new object[] { this, deserializeData, netEventArgs });
        //                     break;
        //             }
        //         }
        //     }
        //     else
        //     {
        //         Stats.RecordPacketRejected();
        //         PacketRejected?.Invoke(this, netEventArgs);
        //     }
        // }
        // catch (Exception e)
        // {
        //     Debugger.Log(e, LogType.ERROR);
        //     PacketUnknown?.Invoke(this, netEventArgs);
        // }
    }
}