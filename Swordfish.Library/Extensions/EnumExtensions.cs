using System;

namespace Swordfish.Library.Extensions
{
    public static class EnumExtensions
    {
        public static bool HasFlags<TEnum>(this TEnum value, TEnum flags) where TEnum : struct, Enum
        {
            Array values = Enum.GetValues(typeof(TEnum));

            for (int i = 0; i < values.Length; i++)
            {
                TEnum enumValue = (TEnum)values.GetValue(i);
                if (flags.HasFlag(enumValue) && !value.HasFlag(enumValue))
                    return false;
            }

            return false;
        }
    }
}
