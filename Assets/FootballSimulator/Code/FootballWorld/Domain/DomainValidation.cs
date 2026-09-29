using System;
using System.Text.RegularExpressions;

namespace FStudio.FootballWorld.Domain
{
    internal static class DomainValidation
    {
        private static readonly Regex IdPattern =
            new Regex(@"\A[A-Za-z0-9][A-Za-z0-9._-]{0,63}\z", RegexOptions.CultureInvariant);

        public static string Id(string value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (!IdPattern.IsMatch(value))
            {
                throw new ArgumentException(
                    "An ID must contain 1 to 64 ASCII letters, digits, dots, underscores or hyphens, starting with a letter or digit.",
                    parameterName);
            }

            return value;
        }

        public static string Name(string value, string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (string.IsNullOrWhiteSpace(value) || UnicodeLength(value) > 100)
            {
                throw new ArgumentException(
                    "A name must contain 1 to 100 Unicode code points and cannot consist only of whitespace.",
                    parameterName);
            }

            return value;
        }

        public static string Text(string value, int maximum, bool allowEmpty, string parameterName)
        {
            if (value == null) throw new ArgumentNullException(parameterName);
            if ((!allowEmpty && string.IsNullOrWhiteSpace(value)) || UnicodeLength(value) > maximum)
                throw new ArgumentException("Text exceeds its limit or is empty.", parameterName);
            return value;
        }

        public static string Code(string value, int length, string parameterName)
        {
            if (value == null || value.Length != length) throw new ArgumentException("Invalid code length.", parameterName);
            foreach (var character in value)
                if (character < 'A' || character > 'Z') throw new ArgumentException("Use uppercase ASCII letters.", parameterName);
            return value;
        }

        public static string HttpUrl(string value, string parameterName)
        {
            Text(value, 2048, false, parameterName);
            if (!Regex.IsMatch(value, @"\Ahttps?://[^\u0009-\u000D\u0020\u0085\u00A0\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000\uFEFF/?#]+(?:[/?#][^\u0009-\u000D\u0020\u0085\u00A0\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000\uFEFF]*)?\z", RegexOptions.CultureInvariant))
                throw new ArgumentException("Use an absolute HTTP(S) source URL without whitespace.", parameterName);
            return value;
        }

        private static int UnicodeLength(string value)
        {
            var count = 0;
            for (var i = 0; i < value.Length; i++, count++)
                if (char.IsHighSurrogate(value[i]) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1])) i++;
            return count;
        }

        public static int InRange(int value, int minimum, int maximum, string parameterName)
        {
            if (value < minimum || value > maximum)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName, value, $"The value must be between {minimum} and {maximum}.");
            }

            return value;
        }
    }
}
