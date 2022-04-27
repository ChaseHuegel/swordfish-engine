using System;
using System.Collections.Generic;
using System.Reflection;

using Swordfish.Library.Extensions;
using Swordfish.Library.Networking.Attributes;
using Swordfish.Library.Networking.Interfaces;
using Swordfish.Library.Types;

namespace Swordfish.Library.Networking
{
    public class PacketManager
    {
        private static PacketManager s_Instance;
        private static PacketManager Instance => s_Instance ?? (s_Instance = Initialize());

        private SwitchDictionary<int, Type, PacketDefinition> PacketDefinitions;

        public static PacketManager Initialize()
        {
            if (s_Instance != null)
            {
                Console.WriteLine("Tried to re-initialize PacketManager while an instance already exists.");
                return s_Instance;
            }

            s_Instance = new PacketManager();
            RegisterPackets();
            RegisterHandlers();
            return s_Instance;
        }

        private static string TruncateToString(object obj) => obj.ToString().TruncateStartUpTo(32).Trim('.').Prepend("...");

        private static void RegisterHandlers()
        {
            Console.WriteLine("Registering packet handlers...");
            Dictionary<int, List<MethodInfo>> handlers = new Dictionary<int, List<MethodInfo>>();

            foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
            foreach (MethodInfo method in type.GetMethods())
            {
                PacketHandlerAttribute packetHandlerAttribute = method.GetCustomAttribute<PacketHandlerAttribute>();
                if (packetHandlerAttribute != null)
                {
                    if (IsValidHandlerParameters(method.GetParameters()))
                    {
                        if (packetHandlerAttribute.PacketType == null)
                            packetHandlerAttribute.PacketType = method.DeclaringType;

                        GetPacketDefinition(packetHandlerAttribute.PacketType).Handlers.Add(new PacketHandler(method, packetHandlerAttribute));
                        Console.WriteLine(
                            $"Registered '{TruncateToString($"{method.DeclaringType}.{method.Name}")}'"
                            + $" to '{TruncateToString(packetHandlerAttribute.PacketType)}'");
                    }
                    else
                    {
                        Console.WriteLine($"Ignored '{TruncateToString(method.DeclaringType)}' decorated as a PacketHandler with invalid signature.");
                    }
                }
            }
        }

        private static void RegisterPackets()
        {
            Console.WriteLine("Registering packets...");
            SwitchDictionary<int, Type, PacketDefinition> packetDefinitions = new SwitchDictionary<int, Type, PacketDefinition>();

            foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
            {
                PacketAttribute packetAttribute = type.GetCustomAttribute<PacketAttribute>();
                if (packetAttribute != null)
                {
                    if (typeof(ISerializedPacket).IsAssignableFrom(type))
                    {
                        ushort id = (ushort)(packetAttribute.PacketID ?? type.FullName.ToSeed());
                        PacketDefinition definition = new PacketDefinition {
                            ID = id,
                            Type = type,
                            RequiresSession = packetAttribute.RequiresSession
                        };

                        packetDefinitions.Add(id, type, definition);
                        Console.WriteLine($"Registered '{definition}'");
                    }
                    else
                    {
                        Console.WriteLine($"Ignored '{type}' decorated as a packet but does not implement {typeof(ISerializedPacket)}");
                    }
                }
            }

            Instance.PacketDefinitions = packetDefinitions;
        }

        public static PacketDefinition GetPacketDefinition(int id) => Instance.PacketDefinitions[id];

        public static PacketDefinition GetPacketDefinition(Type type) => Instance.PacketDefinitions[type];

        public static PacketDefinition GetPacketDefinition(ISerializedPacket packet) => Instance.PacketDefinitions[packet.GetType()];

        private static bool IsValidHandlerParameters(ParameterInfo[] parameters)
        {
            return parameters.Length == 3
                && (parameters[0].ParameterType == typeof(NetController) || parameters[0].ParameterType.BaseType == typeof(NetController))
                && typeof(ISerializedPacket).IsAssignableFrom(parameters[1].ParameterType)
                && parameters[2].ParameterType == typeof(NetEventArgs);
        }
    }
}
