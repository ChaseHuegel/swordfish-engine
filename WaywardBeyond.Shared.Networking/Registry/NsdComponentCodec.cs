using System;
using System.Linq.Expressions;
using System.Reflection;
using Swordfish.ECS;

namespace WaywardBeyond.Shared.Networking.Registry;

/// <summary>
/// Default payload codec for components that are nsd messages. It drives the generated
/// <c>Serialize()</c>/<c>Deserialize(ReadOnlySpan&lt;byte&gt;)</c> methods emitted onto the
/// (partial) component struct by <c>nsdc</c>.
/// </summary>
public sealed class NsdComponentCodec<T> : IPayloadCodec<T>
    where T : struct, IDataComponent
{
    private static readonly Func<T, byte[]> SerializeDelegate;
    private static readonly Func<ReadOnlySpan<byte>, T> DeserializeDelegate;

    static NsdComponentCodec()
    {
        MethodInfo serialize = typeof(T).GetMethod("Serialize", Type.EmptyTypes)
            ?? throw CreateMissingSerializer();
        MethodInfo deserialize = typeof(T).GetMethod("Deserialize", new[] { typeof(ReadOnlySpan<byte>) })
            ?? throw CreateMissingSerializer();

        ParameterExpression instance = Expression.Parameter(typeof(T), "value");
        SerializeDelegate = Expression.Lambda<Func<T, byte[]>>(Expression.Call(instance, serialize), instance).Compile();

        ParameterExpression buffer = Expression.Parameter(typeof(ReadOnlySpan<byte>), "payload");
        DeserializeDelegate = Expression.Lambda<Func<ReadOnlySpan<byte>, T>>(Expression.Call(deserialize, buffer), buffer).Compile();
    }

    public Type ComponentType => typeof(T);

    public byte[] Serialize(DataStore store, int entity)
    {
        if (!store.TryGet(entity, out T component))
        {
            return [];
        }

        return SerializeDelegate(component);
    }

    public void Apply(DataStore store, int entity, ReadOnlySpan<byte> payload)
    {
        store.AddOrUpdate(entity, DeserializeDelegate(payload));
    }

    byte[] IPayloadCodec<T>.Serialize(in T value) => SerializeDelegate(value);

    T IPayloadCodec<T>.Deserialize(ReadOnlySpan<byte> payload) => DeserializeDelegate(payload);

    private static InvalidOperationException CreateMissingSerializer()
    {
        return new InvalidOperationException(
            $"{typeof(T).FullName} is registered as a networked component but does not expose the "
            + "nsd-generated Serialize()/Deserialize methods. Define it as an nsd message."
        );
    }
}