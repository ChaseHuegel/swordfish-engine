using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Swordfish.Library.Extensions
{
    public static class StringExtensions
    {
        public static string Truncate(this string value, int count, int minLength = 0)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            return value.Substring(0, Math.Min(value.Length, minLength > 0 ? Math.Min(count, minLength) : count));
        }

        public static string TruncateStart(this string value, int count, int minLength = 0)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            int start = Math.Min(value.Length, count);
            start = Math.Min(start, value.Length - Math.Min(value.Length, minLength));
            return value.Substring(start, Math.Max(value.Length - start, value.Length - count));
        }

        public static string TruncateUpTo(this string value, int minLength)
        {
            return Truncate(value, int.MaxValue, minLength);
        }

        public static string TruncateStartUpTo(this string value, int minLength)
        {
            return TruncateStart(value, int.MaxValue, minLength);
        }

        public static string Append(this string value, string append)
        {
            return value.Insert(value.Length, append);
        }

        public static string Prepend(this string value, string prepend)
        {
            return value.Insert(0, prepend);
        }

        public static string Envelope(this string value, string envelope)
        {
            return value.Append(envelope).Prepend(envelope);
        }

        public static int ToSeed(this string value)
        {
            int seed = value.Length;
            foreach (char c in value)
                seed = ((seed << 5) + seed) ^ c;

            return seed;
        }

        public static bool IsAlphaNumeric(this string value, params char[] whitelist)
        {
            return value.All(c => Char.IsLetterOrDigit(c) || whitelist.Contains(c));
        }

        public static bool IsAlphabetic(this string value, params char[] whitelist)
        {
            return value.All(c => Char.IsLetter(c) || whitelist.Contains(c));
        }

        public static string Without(this string value, params char[] without)
        {
            foreach (char entry in without)
                value = String.Join(string.Empty, value.Split(entry));

            return value;
        }

        public static string ToTitle(this string value)
        {
            return string.Join(" ",
                Regex.Matches(value, @"([A-Z]+(?![a-z])|[A-Z][a-z]+|[0-9]+|[a-z]+)")
                .OfType<Match>()
                .Select(match => match.Value)
            );
        }

        public static string Substitute(this string value, char newChar, params char[] oldChars)
        {
            return value.Substitute(newChar.ToString(), oldChars);
        }

        public static string Substitute(this string value, string newString, params char[] oldChars)
        {
            string[] splitValue = value.Split(oldChars);
            return string.Join(newString, splitValue);
        }
    }
}
