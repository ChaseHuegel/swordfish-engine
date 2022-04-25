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
        /// The default <see cref="Host"/> to communicate with
        /// if overrides aren't provided to <see cref="Send"/>.
        /// </summary>
        public Host DefaultHost { get; set; }

        /// <summary>
        /// The current session of this <see cref="NetController"/>.
        /// </summary>
        public NetSession Session { get; private set; }

        public EventHandler<NetEventArgs> PacketSent;
        public EventHandler<NetEventArgs> PacketAccepted;
        public EventHandler<NetEventArgs> PacketReceived;
        public EventHandler<NetEventArgs> PacketRejected;
        public EventHandler<NetEventArgs> SessionStarted;
        public EventHandler<NetEventArgs> SessionEnded;
        public EventHandler<NetEventArgs> SessionRejected;

        public ICollection<NetSession> GetSessions() => Sessions.Values;

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
            Session = AddSession((IPEndPoint)Udp.Client.LocalEndPoint);

            DefaultHost = host ?? new Host{
                Address = Session.EndPoint.Address,
                Port = Session.EndPoint.Port
            };

            Udp.BeginReceive(new AsyncCallback(OnReceive), null);
            Console.WriteLine($"NetController session started [{Session}]");
        }

        private void OnSend(IAsyncResult result)
        {
            Udp.EndSend(result);
            PacketSent.Invoke(this, (NetEventArgs) result.AsyncState);
            
            //  TODO If it isn't a fire and forget packet, we should resend with a delay until a response is received
        }

        private void OnReceive(IAsyncResult result)
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
            Packet packet = Udp.EndReceive(result, ref endPoint);

            int sessionID = packet.ReadInt();
            int packetID = packet.ReadInt();
            PacketDefinition packetDefinition = PacketManager.GetPacketDefinition(packetID);

            PacketReceived?.Invoke(endPoint, new NetEventArgs {
                Packet = packet,
                EndPoint = endPoint
            });

            NetSession session = null;
            
            //  The packet is accepted if:
            //  -   the packet doesn't require a session
            //  -   OR the provided session is valid
            if (!packetDefinition.RequiresSession || IsSessionValid(endPoint, sessionID, out session))
            {
                NetEventArgs netEventArgs = new NetEventArgs {
                    Packet = packet,
                    EndPoint = endPoint,
                    Session = session
                };

                PacketAccepted?.Invoke((object) session ?? endPoint, netEventArgs);

                //  Deserialize the packet and invoke it's handlers
                object deserializedPacket = (ISerializedPacket) packet.Deserialize(packetDefinition.Type);
                foreach (MethodInfo handler in packetDefinition.Handlers)
                    handler.Invoke(null, new object[] { this, deserializedPacket, netEventArgs });
            }
            else
            {
                PacketRejected?.Invoke(endPoint, new NetEventArgs {
                    Packet = packet,
                    EndPoint = endPoint
                });
            }

            //  Continue receiving data
            Udp.BeginReceive(new AsyncCallback(OnReceive), null);
        }

        private bool IsSessionValid(IPEndPoint endPoint, int sessionID, out NetSession netSession)
        {
            bool validEndpoint = Sessions.TryGetValue(endPoint, out NetSession validSession);
            netSession = validSession;
            return validEndpoint && sessionID == validSession.ID;
        }

        private Packet SignPacket(ISerializedPacket value)
        {
            return Packet.Create()
                    .Write(Session.ID)
                    .Write(PacketManager.GetPacketDefinition(value).ID)
                    .Serialize(value);
        }

        public void Send(ISerializedPacket value)
        {
            if (string.IsNullOrEmpty(DefaultHost.Hostname))
                Send(SignPacket(value), DefaultHost.EndPoint.Address, DefaultHost.EndPoint.Port);
            else
                Send(SignPacket(value), DefaultHost.Hostname, DefaultHost.Port);
        }

        public void Send(ISerializedPacket value, NetSession session) => Send(SignPacket(value), session.EndPoint.Address, session.EndPoint.Port);

        public void Send(ISerializedPacket value, IPEndPoint endPoint) => Send(SignPacket(value), endPoint.Address, endPoint.Port);

        public void Send(ISerializedPacket value, IPAddress address, int port) => Send(SignPacket(value), address, port);

        public void Send(ISerializedPacket value, string hostname, int port) => Send(SignPacket(value), hostname, port);

        public void Send(byte[] buffer, IPAddress address, int port)
        {
            if (EndPoint == null)
                EndPoint = new IPEndPoint(address, port);
            
            EndPoint.Address = address;
            EndPoint.Port = port;

            NetEventArgs netEventArgs = new NetEventArgs {
                Packet = buffer,
                EndPoint = EndPoint
            };

            Udp.BeginSend(buffer, buffer.Length, EndPoint, OnSend, netEventArgs);
        }

        public void Send(byte[] buffer, string hostname, int port)
        {
            IPEndPoint endPoint = new IPEndPoint(NetUtils.GetHostAddress(hostname), port);
            NetEventArgs netEventArgs = new NetEventArgs {
                Packet = buffer,
                EndPoint = endPoint
            };

            Udp.BeginSend(buffer, buffer.Length, hostname, port, OnSend, netEventArgs);
        }

        public NetSession AddSession(IPEndPoint endPoint)
        {
            NetSession session = new NetSession {
                EndPoint = endPoint,
                ID = Sessions.Count //  TODO recycle session IDs
            };

            if (Sessions.TryAdd(endPoint, session))
            {
                SessionStarted?.Invoke(endPoint, new NetEventArgs {
                    EndPoint = endPoint,
                    Session = session
                });
            }
            else
            {
                SessionRejected?.Invoke(endPoint, new NetEventArgs {
                    EndPoint = endPoint,
                    Session = session
                });
            }

            return session;
        }
    }
}
