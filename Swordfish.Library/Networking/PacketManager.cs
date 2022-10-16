using System;
using System.Reflection;

using Needlefish;
using Swordfish.Library.Collections;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Extensions;
using Swordfish.Library.Networking.Attributes;

namespace Swordfish.Library.Networking
{
    public class PacketManager
    {
        private static SwitchDictionary<int, Type, PacketDefinition> PacketDefinitions = new SwitchDictionary<int, Type, PacketDefinition>();

        static PacketManager()
        {
            RegisterAssembly(Assembly.GetExecutingAssembly());
            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
        }

        public static void RegisterPacketDefinition(int id, PacketDefinition definition)
        {
            PacketDefinitions.Add(id, definition.Type, definition);
        }

        public static void RegisterAssembly() => RegisterAssembly(Assembly.GetCallingAssembly());

        public static void RegisterAssembly<T>() => RegisterAssembly(Assembly.GetAssembly(typeof(T)));

        public static void RegisterAssembly(Type type) => RegisterAssembly(Assembly.GetAssembly(type));

        public static void RegisterAssembly(Assembly assembly)
        {
            RegisterPackets(assembly);
            RegisterHandlers(assembly);
        }

        private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            RegisterAssembly(args.LoadedAssembly);
        }

        private static string TruncateToString(object obj) => obj.ToString().TruncateStartUpTo(32).Trim('.').Prepend("...");

        private static void RegisterHandlers(Assembly assembly)
        {
            bool logged = false;
            foreach (Type type in assembly.GetTypes())
                foreach (MethodInfo method in type.GetMethods())
                {
                    PacketHandlerAttribute packetHandlerAttribute = method.GetCustomAttribute<PacketHandlerAttribute>();
                    if (packetHandlerAttribute != null)
                    {
                        if (!logged)
                        {
                            Debugger.Log($"Registering packet handlers from assembly '{assembly}'...");
                            logged = true;
                        }

                        if (IsValidHandlerParameters(method.GetParameters()))
                        {
                            if (packetHandlerAttribute.PacketType == null)
                                packetHandlerAttribute.PacketType = method.DeclaringType is IDataBody ? method.DeclaringType : method.GetParameters()[1].ParameterType;

                            GetPacketDefinition(packetHandlerAttribute.PacketType).Handlers.Add(new PacketHandler(method, packetHandlerAttribute));
                            Debugger.Log($"{TruncateToString($"{method.DeclaringType}.{method.Name}")}" + $" to {TruncateToString(packetHandlerAttribute.PacketType)}", LogType.CONTINUED);
                        }
                        else
                        {
                            Debugger.Log($"Ignoring {TruncateToString(method.DeclaringType)} decorated as a PacketHandler with invalid signature.", LogType.WARNING);
                        }
                    }
                }
        }

        private static void RegisterPackets(Assembly assembly)
        {
            bool logged = false;
            foreach (Type type in assembly.GetTypes())
            {
                PacketAttribute packetAttribute = type.GetCustomAttribute<PacketAttribute>();
                if (packetAttribute != null)
                {
                    if (!logged)
                    {
                        Debugger.Log($"Registering packets from assembly '{assembly}'...");
                        logged = true;
                    }

                    if (typeof(IDataBody).IsAssignableFrom(type))
                    {
                        int id = packetAttribute.PacketID ?? type.FullName.ToSeed();
                        PacketDefinition definition = new PacketDefinition
                        {
                            ID = id,
                            Type = type,
                            RequiresSession = packetAttribute.RequiresSession
                        };

                        PacketDefinitions.Add(id, definition.Type, definition);
                        Debugger.Log(definition, LogType.CONTINUED);
                    }
                    else
                    {
                        Debugger.Log($"Ignoring {type} decorated as a packet but does not implement {typeof(IDataBody)}", LogType.WARNING);
                    }
                }
            }
        }

        public static PacketDefinition GetPacketDefinition(int id) => PacketDefinitions[id];

        public static PacketDefinition GetPacketDefinition(Type type) => PacketDefinitions[type];

        public static PacketDefinition GetPacketDefinition(IDataBody packet) => PacketDefinitions[packet.GetType()];

        private static bool IsValidHandlerParameters(ParameterInfo[] parameters)
        {
            return parameters.Length == 3
                && (parameters[0].ParameterType == typeof(NetController) || parameters[0].ParameterType.BaseType == typeof(NetController))
                && typeof(IDataBody).IsAssignableFrom(parameters[1].ParameterType)
                && parameters[2].ParameterType == typeof(NetEventArgs);
        }
    }
}
