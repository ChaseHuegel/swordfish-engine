namespace Swordfish.Networking;

public interface IDataService<TMessage> : IDataReader<TMessage>, IDataWriter<TMessage>
{
    void Start();
}

public interface IDataService<TData, TMessage> : IDataReader<TData>, IDataWriter<TMessage>
{
    void Start();
}

public interface IDataService : IDataReader<ArraySegment<byte>>, IDataWriter
{
    void Start();
}