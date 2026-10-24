using Swordfish.ECS;

namespace WaywardBeyond.Networking.Codecs;

/// <summary>Typed payload codec for the component <typeparamref name="T"/>.</summary>
public interface IPayloadCodec<T> : IPayloadCodec
    where T : struct, IDataComponent
{
    /// <summary>Serializes a component value.</summary>
    byte[] Serialize(in T value);
}
