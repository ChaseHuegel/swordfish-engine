namespace Swordfish.Library.Serialization;

public interface ISerializer<T>
{
    byte[] Serialize(T value);

    T Deserialize(byte[] data);
}
