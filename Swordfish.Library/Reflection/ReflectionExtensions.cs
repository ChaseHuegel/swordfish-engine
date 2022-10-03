using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Swordfish.Library.Reflection
{
    public static class ReflectionExtensions
    {
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
            bool isPublic = accessors.Any(x => x.IsPublic);

            List<string> parts = new List<string>();
            if (isPublic) parts.Add("public");
            if (accessors.Any(x => x.IsStatic)) parts.Add("static");
            parts.Add(property.PropertyType.Name);

            if (get != null)
            {
                if (!isPublic || (isPublic && !get.IsPublic))
                    parts.Add(get.IsPublic ? "public" : "private");
                parts.Add("get;");
            }

            if (set != null)
            {
                if (!isPublic || (isPublic && !set.IsPublic))
                    parts.Add(set.IsPublic ? "public" : "private");
                parts.Add("set;");
            }

            return string.Join(" ", parts);
        }
    }
}
