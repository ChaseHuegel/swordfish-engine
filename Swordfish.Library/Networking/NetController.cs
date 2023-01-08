using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Timers;
using Needlefish;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Networking.Packets;
using Swordfish.Library.Threading;

namespace Swordfish.Library.Networking
{
    public class NetController
    {
        private class SequencePair
        {
            public uint Sent, Received;
        }

        private class ReliablePacket
        {
            public DateTime Timestamp;
            public int PacketID;
            public uint PacketSequence;
            public byte[] OriginalBuffer;
        }

        private static Func<int, SequencePair> SequencePairFactory = x => new SequencePair();

        private ThreadWorker ThreadWorker { get; set; }

        private UdpClient Udp { get; set; }

        private IPEndPoint EndPoint { get; set; }

        private Timer KeepAliveTimer { get; set; }

        private ConcurrentDictionary<IPEndPoint, NetSession> Sessions { get; set; }

        private ConcurrentDictionary<int, SequencePair> PacketSequences { get; set; }

        private ConcurrentDictionary<IPEndPoint, List<ReliablePacket>> SentReliablePackets { get; set; }

        /// <summary>
        /// The time it takes for a session to expire.
        /// Any communication will refresh a session's expiration.
        /// Zero disables this behavior.
        /// </summary>
        internal TimeSpan SessionExpiration { get; private set; }

        /// <summary>
        /// Returns a snapshot collection of the valid sessions trusted by this <see cref="NetController"/>.
        /// </summary>
        public ICollection<NetSession> GetSessions() => Sessions.Values;

        /// <summary>
        /// The default <see cref="Host"/> to communicate with
        /// if overrides aren't provided to <see cref="Send"/>.
        /// Typically this would be the server for a client.
        /// </summary>
        public Host DefaultHost { get; set; }

        /// <summary>
        /// The maximum number of sessions allowed to be active.
        /// </summary>
        public int MaxSessions { get; set; } = NetControllerSettings.DefaultMaxSessions;

        /// <summary>
        /// The current session of this <see cref="NetController"/>.
        /// </summary>
        public NetSession Session { get; private set; }

        /// <summary>
        /// The number of sessions currently active.
        /// </summary>
        public int SessionCount => Sessions.Count - 1;    // -1 to not count the local session

        /// <summary>
        /// Whether this <see cref="NetController"/> has reached it's max active sessions.
        /// </summary>
        public bool IsFull => SessionCount >= MaxSessions;

        /// <summary>
        /// Whether this <see cref="NetController"/> has any active sessions.
        /// </summary>
        public bool IsConnected => SessionCount > 0;

        /// <summary>
        /// Whether to validate session IDs.
        /// Should be true to ensure a layer of session security.
        /// <para/>
        /// May be false on a client, but recommended to remain true on a server.
        /// </summary>
        public virtual bool ValidateIDs => true;

        /// <summary>
        /// Whether to validate session endpoints.
        /// Should be true to ensure a layer of session security.
        /// <para/>
        /// May be false on a server if <see cref="ValidateIDs"/> is true, but should always remain true on a client.
        /// </summary>
        public virtual bool ValidateEndPoints => true;

        /// <summary>
        /// Returns whether sessions will be validated.
        /// <para/>
        /// true if <see cref="ValidateIDs"/> and/or <see cref="ValidateEndPoints"/>.
        /// </summary>
        public bool ValidateSessions => ValidateIDs || ValidateEndPoints;

        public EventHandler<NetEventArgs> PacketSent;
        public EventHandler<NetEventArgs> PacketAccepted;
        public EventHandler<NetEventArgs> PacketReceived;
        public EventHandler<NetEventArgs> PacketRejected;
        public EventHandler<NetEventArgs> PacketUnknown;
        public EventHandler<NetEventArgs> SessionStarted;
        public EventHandler<NetEventArgs> SessionEnded;
        public EventHandler<NetEventArgs> SessionRejected;
        public EventHandler<NetEventArgs> Connected;
        public EventHandler<NetEventArgs> Disconnected;

        /// <summary>
        /// Initialize a NetController that automatically binds.
        /// This should be used by a client or client-as-server.
        /// </summary>
        public NetController() => Initialize(default);

        /// <summary>
        /// Initialize a NetController that automatically binds and communicates with a host.
        /// This should be used by a client.
        /// </summary>
        /// <param name="defaultHost">the host to communicate with</param>
        public NetController(Host defaultHost) => Initialize(new NetControllerSettings(defaultHost));

        /// <summary>
        /// Initialize a NetController bound to a port.
        /// This should be used by a server or client-as-server.
        /// </summary>
        /// <param name="port">the port to bind to</param>
        public NetController(int port) => Initialize(new NetControllerSettings(port));

        /// <summary>
        /// Initialize a NetController bound to an address and port. 
        /// This should always be used by a server.
        /// </summary>
        /// <param name="address">the <see cref="IPAddress"/> to bind to</param>
        /// <param name="port">the port to bind to</param>
        public NetController(IPAddress address, int port) => Initialize(new NetControllerSettings(address, port));

        /// <summary>
        /// Initialize a NetController using the provided settings.
        /// </summary>
        public NetController(NetControllerSettings settings) => Initialize(settings);

        private void Initialize(NetControllerSettings settings)
        {
            try
            {
                if (settings.Address == null)
                {
                    if (settings.AddressFamily > AddressFamily.Unspecified)
                        Udp = new UdpClient(settings.Port, settings.AddressFamily);
                    else
                        Udp = new UdpClient(settings.Port);
                }
                else
                {
                    //  Bind to provided address and port.
                    IPEndPoint endPoint = new IPEndPoint(settings.Address, settings.Port);
                    Udp = new UdpClient(endPoint);
                }

                PacketSequences = new ConcurrentDictionary<int, SequencePair>();
                SentReliablePackets = new ConcurrentDictionary<IPEndPoint, List<ReliablePacket>>();

                InitializeSessions();

                //  If a host isn't provided, default to ourself/the local host
                DefaultHost = settings.DefaultHost ?? new Host
                {
                    Address = Session.EndPoint.Address,
                    Port = Session.EndPoint.Port
                };

                ThreadWorker = new ThreadWorker(Heartbeat, false, $"NetController ({GetType()})")
                {
                    TargetTickRate = settings.TickRate > 0 ? settings.TickRate : NetControllerSettings.DefaultTickRate
                };

                if (settings.KeepAlive.TotalMilliseconds > 0)
                {
                    KeepAliveTimer = new Timer(settings.KeepAlive.TotalMilliseconds)
                    {
                        AutoReset = true
                    };
                    KeepAliveTimer.Elapsed += OnKeepAlive;
                    KeepAliveTimer.Start();
                }

                SessionExpiration = settings.SessionExpiration;

                Udp.BeginReceive(new AsyncCallback(OnReceive), null);
                Debugger.Log($"NetController session started [{Session}] with settings [{settings}]");
            }
            catch (Exception ex)
            {
                Debugger.Log($"NetController failed to start with settings [{settings}]\n{ex}", LogType.ERROR);
            }
        }

        private void InitializeSessions()
        {
            if (Sessions != null)
            {
                foreach (NetSession session in Sessions.Values)
                {
                    if (session.ID != NetSession.LocalOrUnassigned)
                        SessionEnded?.Invoke(this, new NetEventArgs
                        {
                            EndPoint = session.EndPoint,
                            Session = session
                        });
                }
            }

            Sessions = new ConcurrentDictionary<IPEndPoint, NetSession>();

            //  Ensure the local connection is assigned a session.
            TryAddSession((IPEndPoint)Udp.Client.LocalEndPoint, out NetSession localSession);
            Session = localSession;
        }

        private void Heartbeat(float deltaTime)
        {
            //  Handle following up on reliable packets that are outstanding
            foreach (KeyValuePair<IPEndPoint, List<ReliablePacket>> item in SentReliablePackets)
            {
                for (int i = 0; i < item.Value.Count; i++)
                {
                    ReliablePacket reliablePacket = item.Value[i];
                    TimeSpan timeElapsed = DateTime.UtcNow - reliablePacket.Timestamp;
                    if (timeElapsed.Milliseconds > 200)
                    {
                        SendRaw(reliablePacket.OriginalBuffer, item.Key);
                    }
                }
            }
        }

        private void OnSend(IAsyncResult result)
        {
            Udp.EndSend(result);
            PacketSent?.Invoke(this, (NetEventArgs)result.AsyncState);

            //  TODO If it isn't a fire and forget packet, we should resend with a delay until a response is received
        }

        private void OnReceive(IAsyncResult result)
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);

            byte[] buffer;
            Packet packet;
            try
            {
                buffer = Udp.EndReceive(result, ref endPoint);
                packet = NeedlefishFormatter.Deserialize<Packet>(buffer);
            }
            catch (SocketException)
            {
                //  There was a connection issue, continue receiving data.
                Udp.BeginReceive(new AsyncCallback(OnReceive), null);
                return;
            }

            NetEventArgs netEventArgs = new NetEventArgs
            {
                Packet = packet,
                EndPoint = endPoint
            };

            try
            {
                PacketDefinition packetDefinition = PacketManager.GetPacketDefinition(packet.PacketID);
                SequencePair currentSequence = PacketSequences.GetOrAdd(packetDefinition.ID, SequencePairFactory);

                netEventArgs.PacketID = packet.PacketID;

                PacketReceived?.Invoke(this, netEventArgs);

                //  The packet is accepted if:
                //  -   the packet doesn't require a session
                //  -   OR the provided session is valid
                //  -   AND the sequence is new IF the packet is ordered
                NetSession session = null;
                if ((!packetDefinition.RequiresSession || IsSessionValid(endPoint, packet.SessionID, out session))
                    && (!packetDefinition.Ordered || packet.Sequence >= currentSequence.Received))
                {
                    //  Any valid communication should refresh sessions.
                    session?.RefreshExpiration();

                    //  Update this packet sequence
                    currentSequence.Received = packet.Sequence;

                    //  Deserialize the data and invoke the packet's handlers
                    IDataBody deserializeData = NeedlefishFormatter.Deserialize(packetDefinition.Type, buffer);

                    //  Process reliable packets
                    if (packetDefinition.Reliable)
                    {
                        //  If it is an ack, then one of our reliable packets has been received.
                        if (packetDefinition.Type == typeof(AckPacket))
                        {
                            AckPacket ack = (AckPacket)deserializeData;
                            UnregisterReliablePacket(endPoint, ack.AckPacketID, ack.AckSequence);
                        }
                        //  Else, we should ack this packet.
                        else
                        {
                            Send(AckPacket.New(packet.PacketID, packet.Sequence), endPoint);
                        }
                    }

                    netEventArgs.Packet = (Packet)deserializeData;
                    netEventArgs.Session = session;
                    PacketAccepted?.Invoke(this, netEventArgs);

                    foreach (PacketHandler handler in packetDefinition.Handlers)
                    {
                        switch (handler.Type)
                        {
                            case PacketHandlerType.SERVER:
                                if (this is NetServer) handler.Method.Invoke(null, new object[] { this, deserializeData, netEventArgs });
                                break;
                            case PacketHandlerType.CLIENT:
                                if (this is NetClient) handler.Method.Invoke(null, new object[] { this, deserializeData, netEventArgs });
                                break;
                            case PacketHandlerType.AGNOSTIC:
                                handler.Method.Invoke(null, new object[] { this, deserializeData, netEventArgs });
                                break;
                        }
                    }
                }
                else
                {
                    PacketRejected?.Invoke(this, netEventArgs);
                }
            }
            catch (Exception e)
            {
                Debugger.Log(e, LogType.ERROR);
                PacketUnknown?.Invoke(this, netEventArgs);
            }

            //  Continue receiving data
            Udp.BeginReceive(new AsyncCallback(OnReceive), null);
        }

        private void OnKeepAlive(object sender, ElapsedEventArgs e)
        {
            Broadcast<PingPacket>();
        }

        private bool IsSessionValid(IPEndPoint endPoint, int sessionID, out NetSession netSession)
        {
            netSession = null;
            bool validEndpoint = !ValidateEndPoints || Sessions.TryGetValue(endPoint, out netSession);
            bool validID = !ValidateIDs || sessionID == netSession?.ID;
            return validEndpoint && validID;
        }

        private void RegisterReliablePacket(IPEndPoint endPoint, Packet packet, byte[] buffer)
        {
            List<ReliablePacket> ReliablePacketListFactory(IPEndPoint arg) => new List<ReliablePacket>();
            List<ReliablePacket> reliablePackets = SentReliablePackets.GetOrAdd(endPoint, ReliablePacketListFactory);
            ReliablePacket reliablePacket = new ReliablePacket
            {
                Timestamp = DateTime.UtcNow,
                PacketID = packet.PacketID,
                PacketSequence = packet.Sequence,
                OriginalBuffer = buffer
            };

            reliablePackets.Add(reliablePacket);
        }

        private void UnregisterReliablePacket(IPEndPoint endPoint, int packetID, uint sequence)
        {
            if (SentReliablePackets.TryGetValue(endPoint, out List<ReliablePacket> reliablePackets))
            {
                for (int i = 0; i < reliablePackets.Count; i++)
                {
                    ReliablePacket reliablePacket = reliablePackets[i];
                    if (reliablePacket.PacketID == packetID && reliablePacket.PacketSequence == sequence)
                    {
                        reliablePackets.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        private byte[] SignPacket(Packet packet, out PacketDefinition packetDefinition)
        {
            packetDefinition = PacketManager.GetPacketDefinition(packet);
            SequencePair currentSequence = PacketSequences.GetOrAdd(packetDefinition.ID, SequencePairFactory);

            packet.SessionID = Session.ID;
            packet.PacketID = packetDefinition.ID;
            //  Ensure we increment the sequence for this packet type
            packet.Sequence = currentSequence.Sent++;

            return NeedlefishFormatter.Serialize(packet);
        }

        public void Send(Packet packet)
        {
            if (string.IsNullOrEmpty(DefaultHost.Hostname))
                Send(packet, DefaultHost.EndPoint.Address, DefaultHost.EndPoint.Port);
            else
                Send(packet, NetUtils.GetHostAddress(DefaultHost.Hostname), DefaultHost.Port);
        }

        public void Send(Packet packet, NetSession session) => Send(packet, session.EndPoint.Address, session.EndPoint.Port);

        public void Send(Packet packet, IPEndPoint endPoint) => Send(packet, endPoint.Address, endPoint.Port);

        public void Send(Packet packet, string hostname, int port) => Send(packet, NetUtils.GetHostAddress(hostname), port);

        public void Send(Packet packet, IPAddress address, int port)
        {
            if (EndPoint == null)
                EndPoint = new IPEndPoint(address, port);

            EndPoint.Address = address;
            EndPoint.Port = port;

            NetEventArgs netEventArgs = new NetEventArgs
            {
                Packet = packet,
                PacketID = packet.PacketID,
                EndPoint = EndPoint
            };

            byte[] buffer = SignPacket(packet, out PacketDefinition packetDefinition);

            if (packetDefinition.Reliable)
                RegisterReliablePacket(EndPoint, packet, buffer);

            SendRaw(buffer, EndPoint, netEventArgs);
        }

        private void SendRaw(byte[] buffer, IPEndPoint endPoint, NetEventArgs netEventArgs = null)
        {
            if (netEventArgs == null)
            {
                netEventArgs = new NetEventArgs
                {
                    Packet = new Packet(),
                    PacketID = 0,
                    EndPoint = EndPoint
                };
            }

            Udp.BeginSend(buffer, buffer.Length, endPoint, OnSend, netEventArgs);
        }

        public void Broadcast<T>() where T : Packet, new()
        {
            Broadcast(new T());
        }

        public void Broadcast(Packet packet)
        {
            foreach (NetSession session in Sessions.Values)
                if (session != Session) Send(packet, session);
        }

        public void BroadcastTo(Packet packet, params NetSession[] whitelist)
        {
            foreach (NetSession session in whitelist)
                if (session != Session) Send(packet, session);
        }

        public void BroadcastExcept(Packet packet, params NetSession[] blacklist)
        {
            foreach (NetSession session in Sessions.Values.Except(blacklist))
                if (session != Session) Send(packet, session);
        }

        public void Disconnect()
        {
            if (IsConnected)
            {
                Debugger.Log($"NetController session [{Session}] disconnected.");
                Broadcast<DisconnectPacket>();
                InvokeLocalDisconnect();
            }
            else
            {
                Debugger.Log("Tried to disconnect but there are no active sessions.", LogType.WARNING);
            }
        }

        internal void InvokeLocalDisconnect()
        {
            InitializeSessions();
            Disconnected?.Invoke(this, NetEventArgs.Empty);
        }

        /// <summary>
        /// Attempts to add a valid <see cref="NetSession"/> from an <see cref="IPEndPoint"/> with a specific ID.
        /// </summary>
        /// <param name="endPoint">the <see cref="IPEndPoint"/> to validate</param>
        /// <param name="id">the id of the session</param>
        /// <param name="session">the session that was added; otherwise null</param>
        /// <returns>true if the session was added; otherwise false</returns>
        public bool TryAddSession(IPEndPoint endPoint, int id, out NetSession session)
        {
            if (TryAddSession(endPoint, out session))
            {
                session.ID = id;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Creates and adds a valid <see cref="NetSession"/> from an <see cref="IPEndPoint"/>.
        /// </summary>
        /// <param name="endPoint">the <see cref="IPEndPoint"/> to validate</param>
        /// <param name="session">the session that was created</param>
        /// <returns>true if the session was added; otherwise false</returns>
        public bool TryAddSession(IPEndPoint endPoint, out NetSession session)
        {
            session = new NetSession(this)
            {
                EndPoint = endPoint
            };

            if (!IsFull && Sessions.TryAdd(endPoint, session))
            {
                //  TODO recycle session IDs
                session.ID = Sessions.Count - 1;

                if (session.ID != NetSession.LocalOrUnassigned)
                {
                    SessionStarted?.Invoke(this, new NetEventArgs
                    {
                        EndPoint = endPoint,
                        Session = session
                    });
                }

                return true;
            }
            else
            {
                SessionRejected?.Invoke(this, new NetEventArgs
                {
                    EndPoint = endPoint
                });

                return false;
            }
        }

        /// <summary>
        /// Attempts to remove a <see cref="NetSession"/> with a specific ID.
        /// </summary>
        /// <param name="id">the id of the session to remove</param>
        /// <returns>true if the session was removed; otherwise false</returns>
        public bool TryRemoveSession(int id)
        {
            NetSession session = Sessions.FirstOrDefault(record => record.Value.ID == id).Value;
            return session != null && TryRemoveSession(session);
        }

        /// <summary>
        /// Attempts to remove a <see cref="NetSession"/>.
        /// </summary>
        /// <param name="NetSession">the session to remove</param>
        /// <returns>true if the session was removed; otherwise false</returns>
        /// <exception cref="ArgumentNullException">session is null.</exception>
        /// <exception cref="ArgumentException">session is the local session.</exception>
        public bool TryRemoveSession(NetSession session)
        {
            if (session == null)
                throw new ArgumentNullException();
            else if (session.Equals(Session))
                throw new ArgumentException();

            if (Sessions.TryRemove(session.EndPoint, out _))
            {
                if (session.ID != NetSession.LocalOrUnassigned)
                {
                    //  Inform the session holder they've been disconnected.
                    Send(new DisconnectPacket(), session.EndPoint);

                    SessionEnded?.Invoke(this, new NetEventArgs
                    {
                        EndPoint = session.EndPoint,
                        Session = session
                    });
                }

                return true;
            }

            return false;
        }
    }
}
