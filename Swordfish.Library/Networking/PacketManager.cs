using System;
using System.Reflection;

using Needlefish;
using Swordfish.Library.Collections;
using Swordfish.Library.Extensions;
using Swordfish.Library.Networking.Attributes;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Networking;

public static class PacketManager
{
    private static readonly SwitchDictionary<int, Type, PacketDefinition> _packetDefinitions = new();

    static PacketManager()
    {
        RegisterAssembly(Assembly.GetExecutingAssembly());
        AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
    }

    public static void RegisterPacketDefinition(int id, PacketDefinition definition)
    {
        _packetDefinitions.Add(id, definition.Type, definition);
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
        var logged = false;
        foreach (Type type in assembly.GetTypes())
        foreach (MethodInfo method in type.GetMethods())
        {
            var packetHandlerAttribute = method.GetCustomAttribute<PacketHandlerAttribute>();
            if (packetHandlerAttribute != null)
            {
                if (!logged)
                {
                    logged = true;
                }

                if (!IsValidHandlerParameters(method.GetParameters()))
                {
                    continue;
                }

                if (packetHandlerAttribute.PacketType == null)
                {
                    packetHandlerAttribute.PacketType = method.DeclaringType is IDataBody ? method.DeclaringType : method.GetParameters()[1].ParameterType;
                }

                GetPacketDefinition(packetHandlerAttribute.PacketType).Handlers.Add(new PacketHandler(method, packetHandlerAttribute));
            }
        }
    }

    private static void RegisterPackets(Assembly assembly)
    {
        var logged = false;
        foreach (Type type in assembly.GetTypes())
        {
            var packetAttribute = type.GetCustomAttribute<PacketAttribute>();
            if (packetAttribute != null)
            {
                if (!logged)
                {
                    logged = true;
                }

                if (!typeof(IDataBody).IsAssignableFrom(type))
                {
                    continue;
                }

                int id = packetAttribute.PacketID ?? type.FullName.ToSeed();
                var definition = new PacketDefinition
                {
                    ID = id,
                    Type = type,
                    RequiresSession = packetAttribute.RequiresSession,
                    Ordered = packetAttribute.Ordered,
                    Reliable = packetAttribute.Reliable,
                };

                _packetDefinitions.Add(id, definition.Type, definition);
            }
        }
    }

    public static PacketDefinition GetPacketDefinition(int id) => _packetDefinitions[id];

    public static PacketDefinition GetPacketDefinition(Type type) => _packetDefinitions[type];

    public static PacketDefinition GetPacketDefinition(IDataBody packet) => _packetDefinitions[packet.GetType()];

    private static bool IsValidHandlerParameters(ParameterInfo[] parameters)
    {
        return parameters.Length == 3
               && (parameters[0].ParameterType == typeof(NetController) || parameters[0].ParameterType.BaseType == typeof(NetController))
               && typeof(IDataBody).IsAssignableFrom(parameters[1].ParameterType)
               && parameters[2].ParameterType == typeof(NetEventArgs);
    }
}