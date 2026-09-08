using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Serialization;
using WaywardBeyond.Shared.Networking.Transport;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// Phase 5: the LAN host accepts multiple remote peers over TCP, wrapping each in its own
/// <see cref="TcpTransport"/> so their frames route independently, and raises <see
/// cref="TcpTransport.OnDisconnected"/> when a peer drops so the host can remove it from its hub.
/// </summary>
public class TcpServerHostTests
{
    private static readonly INetworkSerializer[] _serializers =
    [
        new NsdMessageSerializer<JoinRequest>(),
        new NsdMessageSerializer<JoinAccept>(),
        new NsdMessageSerializer<LeaveGameRequest>(),
    ];

    private static TcpTransport CreateClient(int port)
    {
        var client = new TcpTransport(_serializers, NullLoggerFactory.Instance);
        client.Connect("127.0.0.1", port);
        return client;
    }

    private static Result<T> PollFor<T>(TcpTransport transport, int timeoutMs = 5000)
    {
        var deadline = Environment.TickCount + timeoutMs;
        while (Environment.TickCount < deadline)
        {
            Result<T> result = transport.Receive<T>();
            if (result.Success)
            {
                return result;
            }
            Thread.Sleep(10);
        }
        return Result<T>.FromFailure("Timed out waiting for message.");
    }

    [Fact]
    public void ServerHostAcceptsMultiplePeersWithIndependentRoutingAndDetectsDisconnect()
    {
        using var host = new TcpServerHost(_serializers, NullLoggerFactory.Instance);
        var accepted = new List<TcpTransport>();
        var acceptedGate = new ManualResetEventSlim();
        var disconnectGate = new ManualResetEventSlim();

        host.OnClientAccepted = transport =>
        {
            lock (accepted)
            {
                accepted.Add(transport);
            }

            transport.OnDisconnected = () => disconnectGate.Set();
            acceptedGate.Set();
        };

        host.Listen(0);
        Assert.True(host.LocalPort > 0, "The host should be bound to a port.");

        using TcpTransport clientA = CreateClient(host.LocalPort);
        using TcpTransport clientB = CreateClient(host.LocalPort);

        //  Wait until the host has accepted both peers.
        while (true)
        {
            lock (accepted)
            {
                if (accepted.Count >= 2)
                {
                    break;
                }
            }
            Thread.Sleep(10);
        }

        //  Each client sends a distinct message type; each must land on exactly one accepted peer.
        clientA.Send(new JoinRequest { CharacterId = 11, PublicView = new PublicView { CharacterId = 11, Name = "A", Body = 0 } });
        clientB.Send(new LeaveGameRequest { Dummy = 22 });

        TcpTransport? joinPeer = null;
        TcpTransport? leavePeer = null;
        foreach (TcpTransport peer in accepted)
        {
            //  A's JoinRequest is only delivered to A's socket; B's LeaveGameRequest only to B's.
            Result<JoinRequest> join = PollFor<JoinRequest>(peer);
            Result<LeaveGameRequest> leave = PollFor<LeaveGameRequest>(peer, timeoutMs: 300);

            if (join.Success)
            {
                Assert.Equal(11ul, join.Value.CharacterId);
                Assert.False(leave.Success, "The peer carrying JoinRequest must not also carry LeaveGameRequest.");
                joinPeer = peer;
            }

            if (leave.Success)
            {
                Assert.Equal(22, leave.Value.Dummy);
                Assert.False(join.Success, "The peer carrying LeaveGameRequest must not also carry JoinRequest.");
                leavePeer = peer;
            }
        }

        Assert.NotNull(joinPeer);
        Assert.NotNull(leavePeer);
        Assert.NotSame(joinPeer, leavePeer);

        //  Client A disconnects; the host should observe it on A's accepted peer.
        clientA.Disconnect();
        Assert.True(disconnectGate.Wait(5000), "The host should observe the disconnected peer.");
    }
}