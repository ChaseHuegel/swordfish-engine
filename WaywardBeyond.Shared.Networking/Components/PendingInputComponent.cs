using Swordfish.ECS;

namespace WaywardBeyond.Shared.Networking.Components;

public struct PendingInputComponent : IDataComponent
{
    public const int BufferCapacity = 256;

    public InputComponent[] History;
    public uint Head;
    public uint Tail;

    public PendingInputComponent(int capacity = BufferCapacity)
    {
        History = new InputComponent[capacity];
        Head = 0;
        Tail = 0;
    }

    public void Push(in InputComponent input)
    {
        History[Head % History.Length] = input;
        Head++;
        if (Head - Tail > History.Length)
        {
            Tail = Head - (uint)History.Length;
        }
    }

    public void AckUpTo(uint sequenceNumber)
    {
        while (Tail < Head)
        {
            uint index = Tail % (uint)History.Length;
            if (History[index].SequenceNumber > sequenceNumber)
            {
                break;
            }

            Tail++;
        }
    }

    public readonly int PendingCount => (int)(Head - Tail);

    public readonly InputComponent GetPending(int index)
    {
        return History[(Tail + (uint)index) % (uint)History.Length];
    }
}
