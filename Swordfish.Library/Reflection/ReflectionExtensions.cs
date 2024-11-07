using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Needlefish;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Reflection;

public static class ReflectionExtensions
{
    public static void CacheReflection(this Type type) => Reflection.Cache(type);

    public static bool IsSerializable(this FieldInfo info)
    {
        return !info.IsStatic && !info.IsLiteral && (info.IsPublic || info.GetCustomAttribute<DataFieldAttribute>() != null);
    }

    public static bool IsSerializable(this PropertyInfo info)
    {
        return !info.GetMethod.IsStatic && (info.CanRead || info.GetCustomAttribute<DataFieldAttribute>() != null);
    }

    public static bool IsDeserializable(this FieldInfo info)
    {
        return !info.IsStatic && !info.IsInitOnly && (info.IsPublic || info.GetCustomAttribute<DataFieldAttribute>() != null);
    }

    public static bool IsDeserializable(this PropertyInfo info)
    {
        return (!info.SetMethod?.IsStatic ?? false) && (info.CanWrite || info.GetCustomAttribute<DataFieldAttribute>() != null);
    }

    public static object GetOrder(this MemberInfo info)
    {
        return info.GetCustomAttribute<MemberOrderAttribute>()?.Index ?? int.MaxValue;
    }

    public static bool HasAttribute<TAttribute>(this Type type) where TAttribute : Attribute
    {
        return HasAttribute(type, typeof(TAttribute));
    }

    public static bool HasAttribute(this Type type, Type attribute)
    {
        return Attribute.GetCustomAttribute(type, attribute) != null;
    }

    public static string GetSignature(this FieldInfo field)
    {
        var parts = new List<string>();
        parts.Add(field.IsPublic ? "public" : "private");
        if (field.IsStatic && !field.IsLiteral)
        {
            parts.Add("static");
        }

        if (field.IsLiteral)
        {
            parts.Add("const");
        }

        if (field.IsInitOnly && !field.IsLiteral)
        {
            parts.Add("readonly");
        }

        parts.Add(field.FieldType.Name);
        return string.Join(" ", parts);
    }

    public static string GetSignature(this PropertyInfo property)
    {
        MethodInfo[] accessors = property.GetAccessors(true);
        MethodInfo get = property.GetGetMethod(true);
        MethodInfo set = property.GetSetMethod(true);
        int publicAccessorCount = accessors.Count(methodInfo => methodInfo.IsPublic);
        bool isPublic = publicAccessorCount == accessors.Count();
        bool isPrivate = publicAccessorCount == 0;
        bool hasMixedAccess = !isPublic && !isPrivate;

        var parts = new List<string>();

        parts.Add(hasMixedAccess ? "public" : "private");

        if (accessors.Any(x => x.IsStatic))
        {
            parts.Add("static");
        }

        parts.Add(property.PropertyType.Name);

        if (get != null)
        {
            if (!get.IsPublic && hasMixedAccess)
            {
                parts.Add("private");
            }

            parts.Add("get;");
        }

        if (set == null)
        {
            return string.Join(" ", parts);
        }

        if (!set.IsPublic && hasMixedAccess)
        {
            parts.Add("private");
        }

        parts.Add("set;");

        return string.Join(" ", parts);
    }
}