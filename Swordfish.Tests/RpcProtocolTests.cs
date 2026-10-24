#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NsdRpc;
using Xunit;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Networking.Rpc;

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
        byte[] payload = [];
        int partCount = PayloadFragmenter.PartCount(payload.Length);

        Assert.Equal(1, partCount);
        Assert.Empty(PayloadFragmenter.Slice(payload, 0));
        Assert.Empty(PayloadFragmenter.Join([PayloadFragmenter.Slice(payload, 0)]));
    }

    [Fact]
    public void ExactBoundaryStaysInOnePart()
    {
        var payload = new byte[PayloadFragmenter.FRAGMENT_BUDGET];
        Random.Shared.NextBytes(payload);

        Assert.Equal(1, PayloadFragmenter.PartCount(payload.Length));
        Assert.Equal(payload, PayloadFragmenter.Join([PayloadFragmenter.Slice(payload, 0)]));
    }

    [Fact]
    public void OneByteOverTheBoundarySplitsInTwo()
    {
        var payload = new byte[PayloadFragmenter.FRAGMENT_BUDGET + 1];
        Random.Shared.NextBytes(payload);

        int partCount = PayloadFragmenter.PartCount(payload.Length);

        Assert.Equal(2, partCount);
        Assert.Equal(PayloadFragmenter.FRAGMENT_BUDGET, PayloadFragmenter.Slice(payload, 0).Length);
        Assert.Single(PayloadFragmenter.Slice(payload, 1));
        Assert.Equal(payload, PayloadFragmenter.Join([PayloadFragmenter.Slice(payload, 0), PayloadFragmenter.Slice(payload, 1)]));
    }

    [Fact]
    public void StaleIncompleteGroupIsDropped()
    {
        var assembler = new FragmentAssembler();

        Assert.Equal(FragmentState.Incomplete, assembler.Append(groupId: 1, partIndex: 0, partCount: 2, [1], out _));
        Assert.Equal(FragmentState.Incomplete, assembler.Append(groupId: 2, partIndex: 0, partCount: 2, [2], out _));
        Assert.Equal(FragmentState.Complete, assembler.Append(groupId: 2, partIndex: 1, partCount: 2, [3], out byte[] payload));

        Assert.Equal([2, 3], payload);
        Assert.False(assembler.IsActive);
    }

    [Fact]
    public void SnapshotOverTheBudgetReassemblesAtomically()
    {
        var snapshot = new WorldSnapshot
        {
            TickNumber = 7,
            LastProcessedInput = 3,
            Components = [new ComponentSnapshot(1, 2, new byte[PayloadFragmenter.FRAGMENT_BUDGET])],
            RemovedEntities = [9],
            RemovedComponents = [new ComponentRemoval(9, 2)],
        };

        byte[] payload = snapshot.Serialize();
        int partCount = PayloadFragmenter.PartCount(payload.Length);
        Assert.True(partCount > 1);

        var assembler = new FragmentAssembler();
        WorldSnapshot? joined = null;
        for (var index = 0; index < partCount; index++)
        {
            FragmentState state = assembler.Append(
                groupId: 11,
                partIndex: index,
                partCount: partCount,
                PayloadFragmenter.Slice(payload, index),
                out byte[] joinedPayload);

            if (state == FragmentState.Complete)
            {
                joined = WorldSnapshot.Deserialize(joinedPayload);
            }
        }

        Assert.NotNull(joined);
        Assert.Equal(7u, joined.Value.TickNumber);
        Assert.Equal(3u, joined.Value.LastProcessedInput);
        Assert.Equal(1UL, joined.Value.Components[0].Entity);
        Assert.Equal(PayloadFragmenter.FRAGMENT_BUDGET, joined.Value.Components[0].Payload.Length);
        Assert.Equal(9UL, joined.Value.RemovedEntities[0]);
    }

    [Fact]
    public void OutOfOrderPartResetsTheGroup()
    {
        var assembler = new FragmentAssembler();

        Assert.Equal(FragmentState.Incomplete, assembler.Append(groupId: 1, partIndex: 0, partCount: 2, [1], out _));
        Assert.Equal(FragmentState.Rejected, assembler.Append(groupId: 1, partIndex: 1, partCount: 3, [2], out _));
        Assert.False(assembler.IsActive);
        Assert.Equal(FragmentState.Rejected, assembler.Append(groupId: 1, partIndex: 2, partCount: 3, [3], out _));
    }

    [Fact]
    public void OversizedPartCountIsRejected()
    {
        var assembler = new FragmentAssembler();

        Assert.Equal(
            FragmentState.Rejected,
            assembler.Append(groupId: 1, partIndex: 0, partCount: PayloadFragmenter.MAX_PARTS + 1, [], out _));
        Assert.False(assembler.IsActive);
    }
}
