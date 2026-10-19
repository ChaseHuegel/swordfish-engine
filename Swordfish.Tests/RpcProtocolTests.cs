#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using NsdRpc;
using WaywardBeyond.Networking.Rpc;
using Xunit;

namespace Swordfish.Tests;

public class RpcProtocolTests
{
    [Fact]
    public void MethodIdsAreUniqueAndDirectionsMatch()
    {
        AssertService(typeof(IGameServerApi), RpcDirection.ClientToServer);
        AssertService(typeof(IGameClientApi), RpcDirection.ServerToClient);
    }

    [Fact]
    public void RegistriesFreezeWithBothGeneratedServices()
    {
        var serverRegistry = new RpcRegistry();
        serverRegistry.AddService(new EmptyServerHandler());
        serverRegistry.Freeze();

        var clientRegistry = new RpcRegistry();
        clientRegistry.AddService(new EmptyClientHandler());
        clientRegistry.Freeze();
    }

    [Fact]
    public void MethodConstantsMatchTheDeclaredAttributes()
    {
        var declared = new HashSet<ulong>();
        foreach (MethodInfo method in typeof(IGameServerApi).GetMethods())
        {
            RpcMethodAttribute? attribute = method.GetCustomAttribute<RpcMethodAttribute>();
            if (attribute != null)
            {
                declared.Add(attribute.Method);
            }
        }

        foreach (MethodInfo method in typeof(IGameClientApi).GetMethods())
        {
            RpcMethodAttribute? attribute = method.GetCustomAttribute<RpcMethodAttribute>();
            if (attribute != null)
            {
                declared.Add(attribute.Method);
            }
        }

        ulong[] constants = typeof(GameRpc.Methods)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (ulong)field.GetRawConstantValue()!)
            .ToArray();

        Assert.Equal(constants.OrderBy(id => id), declared.OrderBy(id => id));
    }

    private static void AssertService(Type service, RpcDirection expected)
    {
        var invocationIds = new HashSet<ulong>();
        var handlerIds = new HashSet<ulong>();
        foreach (MethodInfo method in service.GetMethods())
        {
            RpcMethodAttribute? attribute = method.GetCustomAttribute<RpcMethodAttribute>();
            if (attribute == null)
            {
                continue;
            }

            Assert.NotEqual(0UL, attribute.Method);
            Assert.Equal(expected, attribute.Direction);
            Assert.True(IsInRange(attribute.Method), $"Method {attribute.Method} is outside its subject range.");

            //  One invocation shape and one handler shape may share an id. Two shapes of the same
            //  role may not.
            bool isHandler = method.GetParameters().Any(parameter => parameter.ParameterType == typeof(RpcContext));
            HashSet<ulong> roleIds = isHandler ? handlerIds : invocationIds;
            Assert.True(roleIds.Add(attribute.Method), $"Duplicate method id {attribute.Method} on {service.Name}.");
        }

        Assert.NotEmpty(handlerIds);
        Assert.Equal(handlerIds.OrderBy(id => id), invocationIds.OrderBy(id => id));
    }

    private static bool IsInRange(ulong method)
    {
        return method is >= 1 and <= 19
            or >= 20 and <= 59
            or >= 60 and <= 99
            or >= 100 and <= 149
            or >= 200 and <= 249
            or >= 300 and <= 349
            or >= 350 and <= 399;
    }

    private sealed class EmptyServerHandler : GameServerApiHandler { }

    private sealed class EmptyClientHandler : GameClientApiHandler { }
}

public class PayloadFragmenterTests
{
    [Fact]
    public void ZeroLengthPayloadRoundTripsAsOnePart()
    {
        byte[][] parts = PayloadFragmenter.Split([], budget: 4);

        Assert.Single(parts);
        Assert.Empty(parts[0]);
        Assert.Empty(PayloadFragmenter.Join(parts));
    }

    [Fact]
    public void ExactBoundaryStaysInOnePart()
    {
        byte[] payload = [1, 2, 3, 4];

        byte[][] parts = PayloadFragmenter.Split(payload, budget: 4);

        Assert.Single(parts);
        Assert.Equal(payload, PayloadFragmenter.Join(parts));
    }

    [Fact]
    public void OneByteOverTheBoundarySplitsInTwo()
    {
        byte[] payload = [1, 2, 3, 4, 5];

        byte[][] parts = PayloadFragmenter.Split(payload, budget: 4);

        Assert.Equal(2, parts.Length);
        Assert.Equal(4, parts[0].Length);
        Assert.Single(parts[1]);
        Assert.Equal(payload, PayloadFragmenter.Join(parts));
    }

    [Fact]
    public void StaleIncompleteGroupIsDropped()
    {
        var assembler = new FragmentAssembler();

        Assert.False(assembler.TryAppend(groupId: 1, partIndex: 0, partCount: 2, [1], out _));
        Assert.False(assembler.TryAppend(groupId: 2, partIndex: 0, partCount: 2, [2], out _));
        Assert.True(assembler.TryAppend(groupId: 2, partIndex: 1, partCount: 2, [3], out byte[] payload));

        Assert.Equal([2, 3], payload);
        Assert.False(assembler.IsActive);
    }

    [Fact]
    public void OutOfOrderPartResetsTheGroup()
    {
        var assembler = new FragmentAssembler();

        Assert.False(assembler.TryAppend(groupId: 1, partIndex: 0, partCount: 2, [1], out _));
        Assert.False(assembler.TryAppend(groupId: 1, partIndex: 1, partCount: 3, [2], out _));
        Assert.False(assembler.IsActive);
        Assert.False(assembler.TryAppend(groupId: 1, partIndex: 2, partCount: 3, [3], out _));
    }
}
