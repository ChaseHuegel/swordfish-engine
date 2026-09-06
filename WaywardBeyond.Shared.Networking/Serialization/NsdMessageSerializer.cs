using System;
using System.Linq.Expressions;
using System.Reflection;
using Swordfish.Library.Serialization;

namespace WaywardBeyond.Shared.Networking.Serialization;

/// <summary>
/// <see cref="ISerializer{T}"/> over an nsd message. Drives the generated
/// <c>Serialize()</c>/<c>Deserialize(ReadOnlySpan&lt;byte&gt;)</c> methods emitted by <c>nsdc</c>,
/// so no per-message adapter is required.
/// </summary>
public sealed class NsdMessageSerializer<T> : ISerializer<T>, INetworkSerializer
{
    public Type MessageType => typeof(T);

    private static readonly Func<T, byte[]> _serializeDelegate;
    private static readonly Func<ReadOnlySpan<byte>, T> _deserializeDelegate;

    static NsdMessageSerializer()
    {
        MethodInfo serialize = typeof(T).GetMethod("Serialize", Type.EmptyTypes)
            ?? throw new InvalidOperationException($"{typeof(T).FullName} is not an nsd message (missing Serialize()).");
        MethodInfo deserialize = typeof(T).GetMethod("Deserialize", new[] { typeof(ReadOnlySpan<byte>) })
            ?? throw new InvalidOperationException($"{typeof(T).FullName} is not an nsd message (missing Deserialize(ReadOnlySpan<byte>)).");

        ParameterExpression instance = Expression.Parameter(typeof(T), "value");
        _serializeDelegate = Expression.Lambda<Func<T, byte[]>>(Expression.Call(instance, serialize), instance).Compile();

        ParameterExpression buffer = Expression.Parameter(typeof(ReadOnlySpan<byte>), "payload");
        _deserializeDelegate = Expression.Lambda<Func<ReadOnlySpan<byte>, T>>(Expression.Call(deserialize, buffer), buffer).Compile();
    }

    public byte[] Serialize(T message) => _serializeDelegate(message);

    public T Deserialize(byte[] data) => _deserializeDelegate(data);
}