using System.Linq;
using System.Threading.Tasks.Dataflow;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

using Swordfish.Library.Networking.Interfaces;

namespace Swordfish.Library.Networking
{
    public class NetController
    {
        private UdpClient Udp { get; set; }

        private IPEndPoint EndPoint { get; set; }

        private ConcurrentDictionary<IPEndPoint, NetSession> Sessions { get; set; }

        /// <summary>
        /// Returns a snapshot collection of the valid sessions trusted by this <see cref="NetController"/>.
        /// </summary>
        public ICollection<NetSession> GetSessions() => Sessions.Values;

        /// <summary>
        /// The default <see cref="Host"/> to communicate with
        /// if overrides aren't provided to <see cref="Send"/>.
        /// </summary>
        public Host DefaultHost { get; set; }

        /// <summary>
        /// The current session of this <see cref="NetController"/>.
        /// </summary>
        public NetSession Session { get; private set; }

        /// <summary>
        /// The maximum number of sessions allowed to be active.
        /// </summary>
        public int MaxSessions { get; set; } = 20;
        
        /// <summary>
        /// The number of sessions currently active.
        /// </summary>
        public int SessionCount => Sessions.Count-1;    // -1 to not count the local session

        /// <summary>
        /// Whether this <see cref="NetController"/> has reached it's max active sessions.
        /// </summary>
        public bool IsFull => SessionCount >= MaxSessions;

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

        /// <summary>
        /// Initialize a NetController that automatically binds.
        /// This should be used by a client or client-as-server.
        /// </summary>
        public NetController() => Initialize(null, 0, null);

        /// <summary>
        /// Initialize a NetController that automatically binds and communicates with a host.
        /// This should be used by a client.
        /// </summary>
        /// <param name="host">the host to communicate with</param>
        public NetController(Host host) => Initialize(null, 0, host);

        /// <summary>
        /// Initialize a NetController bound to a port.
        /// This should be used by a server or client-as-server.
        /// </summary>
        /// <param name="port">the port to bind to</param>
        public NetController(int port) => Initialize(null, port, null);

        /// <summary>
        /// Initialize a NetController bound to an address and port. 
        /// This should always be used by a server.
        /// </summary>
        /// <param name="address">the <see cref="IPAddress"/> to bind to</param>
        /// <param name="port">the port to bind to</param>
        public NetController(IPAddress address, int port) => Initialize(address, port, null);

        private void Initialize(IPAddress address, int port, Host host)
        {
            try {
                if (address == null)
                {
                    //  Bind automatically if no address or port is provided.
                    if (port <= 0)
                        Udp = new UdpClient(0);
                    //  Bind to a provided port and automatic address.
                    else
                        Udp = new UdpClient(port);
                }
                else
                {
                    //  Bind to provided address and port.
                    var endPoint = new IPEndPoint(address, port);
                    Udp = new UdpClient(endPoint);
                }
                
                //  Setup sessions; ensure the local connection is assigned a session.
                Sessions = new ConcurrentDictionary<IPEndPoint, NetSession>();
                TryAddSession((IPEndPoint)Udp.Client.LocalEndPoint, out NetSession session);
                Session = session;

                DefaultHost = host ?? new Host{
                    Address = Session.EndPoint.Address,
                    Port = Session.EndPoint.Port
                };

                Udp.BeginReceive(new AsyncCallback(OnReceive), null);
                Console.WriteLine($"NetController session started [{Session}]");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"NetController failed to start on [{port}]\n{ex}");
            }
        }

        private void OnSend(IAsyncResult result)
        {
            Udp.EndSend(result);
            PacketSent?.Invoke(this, (NetEventArgs) result.AsyncState);
            
            //  TODO If it isn't a fire and forget packet, we should resend with a delay until a response is received
        }

        private void OnReceive(IAsyncResult result)
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
            Packet packet = Udp.EndReceive(result, ref endPoint);

            NetEventArgs netEventArgs = new NetEventArgs {
                    Packet = packet,
                    EndPoint = endPoint
                };

            try {
                int sessionID = packet.ReadInt();
                int packetID = packet.ReadInt();
                PacketDefinition packetDefinition = PacketManager.GetPacketDefinition(packetID);

                netEventArgs.PacketID = packetID;

                PacketReceived?.Invoke(this, netEventArgs);

                //  The packet is accepted if:
                //  -   the packet doesn't require a session
                //  -   OR the provided session is valid
                NetSession session = null;
                if (!packetDefinition.RequiresSession || IsSessionValid(endPoint, sessionID, out session))
                {
                    netEventArgs.Session = session;
                    PacketAccepted?.Invoke(this, netEventArgs);

                    //  Deserialize the packet and invoke it's handlers
                    object deserializedPacket = (ISerializedPacket) packet.Deserialize(packetDefinition.Type);
                    foreach (PacketHandler handler in packetDefinition.Handlers)
                    {
                        switch (handler.Type)
                        {
                            case PacketHandlerType.SERVER:
                                if (this is NetServer) handler.Method.Invoke(null, new object[] { this, deserializedPacket, netEventArgs });
                                break;
                            case PacketHandlerType.CLIENT:
                                if (this is NetClient) handler.Method.Invoke(null, new object[] { this, deserializedPacket, netEventArgs });
                                break;
                            case PacketHandlerType.AGNOSTIC:
                                handler.Method.Invoke(null, new object[] { this, deserializedPacket, netEventArgs });
                                break;
                        }
                    }
                }
                else
                {
                    PacketRejected?.Invoke(this, netEventArgs);
                }
            }
            catch
            {
                PacketUnknown?.Invoke(this, netEventArgs);
            }

            //  Continue receiving data
            Udp.BeginReceive(new AsyncCallback(OnReceive), null);
        }

        private bool IsSessionValid(IPEndPoint endPoint, int sessionID, out NetSession netSession)
        {
            netSession = null;
            bool validEndpoint = !ValidateEndPoints || Sessions.TryGetValue(endPoint, out netSession);
            bool validID = !ValidateIDs || sessionID == netSession?.ID;
            return validEndpoint && validID;
        }

        private Packet SignPacket(ISerializedPacket value)
        {
            return Packet.Create()
                    .Write(Session.ID)
                    .Write(PacketManager.GetPacketDefinition(value).ID)
                    .Serialize(value);
        }

        public void Send(ISerializedPacket packet)
        {
            if (string.IsNullOrEmpty(DefaultHost.Hostname))
                Send(SignPacket(packet), DefaultHost.EndPoint.Address, DefaultHost.EndPoint.Port);
            else
                Send(SignPacket(packet), DefaultHost.Hostname, DefaultHost.Port);
        }

        public void Send(ISerializedPacket packet, NetSession session) => Send(SignPacket(packet), session.EndPoint.Address, session.EndPoint.Port);

        public void Send(ISerializedPacket packet, IPEndPoint endPoint) => Send(SignPacket(packet), endPoint.Address, endPoint.Port);

        public void Send(ISerializedPacket packet, IPAddress address, int port) => Send(SignPacket(packet), address, port);

        public void Send(ISerializedPacket packet, string hostname, int port) => Send(SignPacket(packet), hostname, port);

        public void Send(byte[] buffer, IPAddress address, int port)
        {
            if (EndPoint == null)
                EndPoint = new IPEndPoint(address, port);
            
            EndPoint.Address = address;
            EndPoint.Port = port;

            Packet packet = buffer;
            int sessionID = packet.ReadInt();
            int packetID = packet.ReadInt();

            NetEventArgs netEventArgs = new NetEventArgs {
                Packet = packet,
                PacketID = packetID,
                EndPoint = EndPoint
            };

            Udp.BeginSend(buffer, buffer.Length, EndPoint, OnSend, netEventArgs);
        }

        public void Send(byte[] buffer, string hostname, int port)
        {
            IPEndPoint endPoint = new IPEndPoint(NetUtils.GetHostAddress(hostname), port);
            Packet packet = buffer;
            int sessionID = packet.ReadInt();
            int packetID = packet.ReadInt();

            NetEventArgs netEventArgs = new NetEventArgs {
                Packet = packet,
                PacketID = packetID,
                EndPoint = endPoint
            };

            Udp.BeginSend(buffer, buffer.Length, hostname, port, OnSend, netEventArgs);
        }

        public void Broadcast(ISerializedPacket packet)
        {
            foreach (NetSession session in Sessions.Values)
                if (session != Session) Send(packet, session);
        }

        public void BroadcastTo(ISerializedPacket packet, params NetSession[] whitelist)
        {
            foreach (NetSession session in whitelist)
                if (session != Session) Send(packet, session);
        }

        public void BroadcastExcept(ISerializedPacket packet, params NetSession[] blacklist)
        {
            foreach (NetSession session in Sessions.Values.Except(blacklist))
                if (session != Session) Send(packet, session);
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
            session = new NetSession {
                EndPoint = endPoint
            };

            if (!IsFull && Sessions.TryAdd(endPoint, session))
            {
                //  TODO recycle session IDs
                session.ID = Sessions.Count-1;

                SessionStarted?.Invoke(this, new NetEventArgs {
                    EndPoint = endPoint,
                    Session = session
                });

                return true;
            }
            else
            {
                SessionRejected?.Invoke(this, new NetEventArgs {
                    EndPoint = endPoint
                });

                return false;
            }
        }
    }
}
