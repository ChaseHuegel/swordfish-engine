using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Needlefish;

namespace Swordfish.Library.Reflection
{
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
            List<string> parts = new List<string>();
            parts.Add(field.IsPublic ? "public" : "private");
            if (field.IsStatic && !field.IsLiteral) parts.Add("static");
            if (field.IsLiteral) parts.Add("const");
            if (field.IsInitOnly && !field.IsLiteral) parts.Add("readonly");
            parts.Add(field.FieldType.Name);
            return string.Join(" ", parts);
        }

        public static string GetSignature(this PropertyInfo property)
        {
            var accessors = property.GetAccessors(true);
            var get = property.GetGetMethod(true);
            var set = property.GetSetMethod(true);
            bool getAndSet = get != null && set != null;
            int publicAccessorCount = accessors.Where(x => x.IsPublic).Count();
            bool isPublic = publicAccessorCount == accessors.Count();
            bool isPrivate = publicAccessorCount == 0;
            bool hasMixedAccess = !isPublic && !isPrivate;

            List<string> parts = new List<string>();

            if (hasMixedAccess)
                parts.Add("public");
            else
                parts.Add("private");

            if (accessors.Any(x => x.IsStatic)) parts.Add("static");

            parts.Add(property.PropertyType.Name);

            if (get != null)
            {
                if (!get.IsPublic && hasMixedAccess)
                    parts.Add("private");
                parts.Add("get;");
            }

            if (set != null)
            {
                if (!set.IsPublic && hasMixedAccess)
                    parts.Add("private");
                parts.Add("set;");
            }

            return string.Join(" ", parts);
        }
    }
}
