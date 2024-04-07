namespace Swordfish.Networking;

public struct DataReceivedArgs<TSource>
{
    public TSource Source;
    public ArraySegment<byte> Data;

    public DataReceivedArgs(TSource source, ArraySegment<byte> data)
    {
        Source = source;
        Data = data;
    }
}