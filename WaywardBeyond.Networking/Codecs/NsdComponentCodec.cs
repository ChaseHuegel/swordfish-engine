using System;
using System.Reflection;
using Swordfish.ECS;
using Swordfish.Library.Util;

namespace WaywardBeyond.Networking.Codecs;

/// <summary>Payload codec for components declared as nsd messages; drives the generated serializers.</summary>
public sealed class NsdComponentCodec<T> : IPayloadCodec<T>
    where T : struct, IDataComponent
{
    private static readonly SerializeDelegate _serialize;
    private static readonly DeserializeDelegate _deserialize;

    static NsdComponentCodec()
    {
        MethodInfo serialize = typeof(T).GetMethod("Serialize", Type.EmptyTypes) ?? throw CreateMissingSerializer();
        MethodInfo deserialize = typeof(T).GetMethod("Deserialize", [typeof(ReadOnlySpan<byte>)]) ?? throw CreateMissingSerializer();

        if (serialize.IsStatic || serialize.ReturnType != typeof(byte[]) || serialize.GetParameters().Length != 0)
        {
            throw CreateMissingSerializer();
        }

        if (!deserialize.IsStatic || deserialize.ReturnType != typeof(T) || deserialize.GetParameters().Length != 1)
        {
            throw CreateMissingSerializer();
        }

        _serialize = serialize.CreateDelegate<SerializeDelegate>();
        _deserialize = deserialize.CreateDelegate<DeserializeDelegate>();
    }

    /// <inheritdoc/>
    public Result<byte[]> Serialize(DataStore store, int entity)
    {
        if (!store.TryGet(entity, out T component))
        {
            return Result<byte[]>.FromFailure((string?)null);
        }

        return Result<byte[]>.FromSuccess(Serialize(in component));
    }

    /// <inheritdoc/>
    public Result Apply(DataStore store, int entity, ReadOnlySpan<byte> payload)
    {
        try
        {
            store.AddOrUpdate(entity, _deserialize(payload));
            return Result.FromSuccess();
        }
        catch (Exception ex)
        {
            return Result.FromFailure(ex);
        }
    }

    /// <inheritdoc/>
    public byte[] Serialize(in T value)
    {
        //  The generated serializer takes an instance, so an open delegate needs a writable copy.
        T copy = value;
        return _serialize(ref copy);
    }

    private delegate byte[] SerializeDelegate(ref T value);

    private delegate T DeserializeDelegate(ReadOnlySpan<byte> payload);

    private static InvalidOperationException CreateMissingSerializer()
    {
        return new InvalidOperationException(
            $"{typeof(T).FullName} is registered as a networked component but does not expose the "
            + "nsd-generated Serialize()/Deserialize methods. Define it as an nsd message."
        );
    }
}
