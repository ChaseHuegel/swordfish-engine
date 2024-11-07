using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Reflection;

public static class Reflection
{
    public const BindingFlags BINDINGS_ALL = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    public const BindingFlags BINDINGS_PUBLIC = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
    public const BindingFlags BINDINGS_PRIVATE = BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    public const BindingFlags BINDINGS_PUBLIC_INSTANCE = BindingFlags.Public | BindingFlags.Instance;
    public const BindingFlags BINDINGS_PRIVATE_INSTANCE = BindingFlags.NonPublic | BindingFlags.Instance;
    public const BindingFlags BINDINGS_PUBLIC_STATIC = BindingFlags.Public | BindingFlags.Static;
    public const BindingFlags BINDINGS_PRIVATE_STATIC = BindingFlags.NonPublic | BindingFlags.Static;

    private static readonly ConcurrentDictionary<TypedBindingFlags, FieldInfo[]> _fields = new();
    private static readonly ConcurrentDictionary<TypedBindingFlags, PropertyInfo[]> _properties = new();

    private static readonly ConcurrentDictionary<Type, FieldInfo[]> _serializableFields = new();

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _serializableProperties = new();

    public static FieldInfo[] GetFields(Type type, BindingFlags bindingFlags = BINDINGS_ALL, bool ignoreBackingFields = false)
    {
        FieldInfo[] FieldInfoFactory(TypedBindingFlags binding)
        {
            return binding.Type.GetFields(binding.BindingFlags).OrderBy(x => x.GetOrder()).ToArray();
        }

        if (ignoreBackingFields)
        {
            return _fields.GetOrAdd(new TypedBindingFlags(type, bindingFlags), FieldInfoFactory)
                .Where(x => x.Name[0] != '<' && x.Name[0] != '_' && (x.Name.Length == 1 || x.Name[1] != '_')).ToArray();
        }

        return _fields.GetOrAdd(new TypedBindingFlags(type, bindingFlags), FieldInfoFactory);
    }

    public static PropertyInfo[] GetProperties(Type type, BindingFlags bindingFlags = BINDINGS_ALL)
    {
        PropertyInfo[] PropertyInfoFactory(TypedBindingFlags binding)
        {
            return binding.Type.GetProperties(binding.BindingFlags).OrderBy(x => x.GetOrder()).ToArray();
        }

        return _properties.GetOrAdd(new TypedBindingFlags(type, bindingFlags), PropertyInfoFactory);
    }

    public static FieldInfo[] GetSerializableFields(Type type)
    {
        FieldInfo[] SerializableFieldInfoFactory(Type fieldType)
        {
            return GetFields(fieldType, BINDINGS_ALL, false).Where(x => x.IsSerializable()).ToArray();
        }

        return _serializableFields.GetOrAdd(type, SerializableFieldInfoFactory);
    }

    public static PropertyInfo[] GetSerializableProperties(Type type)
    {
        PropertyInfo[] SerializablePropertyInfoFactory(Type propertyType)
        {
            return GetProperties(propertyType, BINDINGS_ALL).Where(x => x.IsSerializable()).ToArray();
        }

        return _serializableProperties.GetOrAdd(type, SerializablePropertyInfoFactory);
    }

    public static FieldInfo[] GetDeserializableFields(Type type)
    {
        FieldInfo[] DeserializableFieldInfoFactory(Type fieldType)
        {
            return GetFields(fieldType, BINDINGS_ALL, false).Where(x => x.IsDeserializable()).ToArray();
        }

        return _serializableFields.GetOrAdd(type, DeserializableFieldInfoFactory);
    }

    public static PropertyInfo[] GetDeserializableProperties(Type type)
    {
        PropertyInfo[] DeserializablePropertyInfoFactory(Type propertyType)
        {
            return GetProperties(propertyType, BINDINGS_ALL).Where(x => x.IsDeserializable()).ToArray();
        }

        return _serializableProperties.GetOrAdd(type, DeserializablePropertyInfoFactory);
    }

    public static void Cache(Type type)
    {
        //  GetSerializable[Fields/Properties] implicitly calls Get[Fields/Properties]
        GetSerializableFields(type);
        GetSerializableProperties(type);
    }

    public static bool HasAttribute<TType, TAttribute>() where TAttribute : Attribute
    {
        return HasAttribute(typeof(TType), typeof(TAttribute));
    }

    public static bool HasAttribute<TAttribute>(Type type) where TAttribute : Attribute
    {
        return HasAttribute(type, typeof(TAttribute));
    }

    public static bool HasAttribute(Type type, Type attribute)
    {
        return Attribute.GetCustomAttribute(type, attribute) != null;
    }

    public static bool TryGetAttribute<TType, TAttribute>(out TAttribute result) where TAttribute : Attribute
    {
        return TryGetAttribute(typeof(TType), out result);
    }

    public static bool TryGetAttribute<TAttribute>(Type type, out TAttribute result) where TAttribute : Attribute
    {
        bool hasAttribute = TryGetAttribute(type, typeof(TAttribute), out Attribute attribute);
        result = (TAttribute)attribute;
        return hasAttribute;
    }

    public static bool TryGetAttribute(Type type, Type attribute, out Attribute result)
    {
        result = Attribute.GetCustomAttribute(type, attribute);
        return result != null;
    }
}