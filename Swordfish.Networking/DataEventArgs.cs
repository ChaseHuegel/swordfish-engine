namespace Swordfish.Networking;

public struct DataEventArgs
{
    public ArraySegment<byte> Data;

    public DataEventArgs(ArraySegment<byte> data)
    {
        Data = data;
    }
}