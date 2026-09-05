using System;
using System.Linq.Expressions;
using System.Reflection;

namespace WaywardBeyond.Shared.Networking.Serialization;

/// <summary>
/// <see cref="INetworkSerializer"/> over an nsd message. Drives the generated
/// <c>Serialize()</c>/<c>Deserialize(ReadOnlySpan&lt;byte&gt;)</c> methods emitted by <c>nsdc</c>,
/// so no per-message adapter is required.
/// </summary>
public sealed class NsdMessageSerializer<T> : INetworkSerializer
{
    private static readonly Func<T, byte[]> SerializeDelegate;
    private static readonly Func<ReadOnlySpan<byte>, T> DeserializeDelegate;

    static NsdMessageSerializer()
    {
        MethodInfo serialize = typeof(T).GetMethod("Serialize", Type.EmptyTypes)
            ?? throw new InvalidOperationException($"{typeof(T).FullName} is not an nsd message (missing Serialize()).");
        MethodInfo deserialize = typeof(T).GetMethod("Deserialize", new[] { typeof(ReadOnlySpan<byte>) })
            ?? throw new InvalidOperationException($"{typeof(T).FullName} is not an nsd message (missing Deserialize(ReadOnlySpan<byte>)).");

        ParameterExpression instance = Expression.Parameter(typeof(T), "value");
        SerializeDelegate = Expression.Lambda<Func<T, byte[]>>(Expression.Call(instance, serialize), instance).Compile();

        ParameterExpression buffer = Expression.Parameter(typeof(ReadOnlySpan<byte>), "payload");
        DeserializeDelegate = Expression.Lambda<Func<ReadOnlySpan<byte>, T>>(Expression.Call(deserialize, buffer), buffer).Compile();
    }

    public Type MessageType => typeof(T);

    public byte[] Serialize(object message) => SerializeDelegate((T)message);

    public object Deserialize(ReadOnlySpan<byte> data) => DeserializeDelegate(data);
}