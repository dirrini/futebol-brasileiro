using System;

namespace FStudio.FootballWorld.Infrastructure.Importing
{
    // Json.NET intentionally supports JavaScript extensions. This small grammar check
    // rejects those extensions before its bounded token reader processes the document.
    internal sealed class StrictJsonSyntax
    {
        private readonly string text;
        private readonly int maxDepth;
        private int offset;

        private StrictJsonSyntax(string text, int maxDepth) { this.text = text; this.maxDepth = maxDepth; }

        internal static void Validate(string text, int maxDepth)
        {
            var syntax = new StrictJsonSyntax(text, maxDepth);
            syntax.Value(0);
            syntax.Whitespace();
            if (syntax.offset != text.Length) syntax.Fail();
        }

        private void Value(int depth)
        {
            Whitespace();
            if (offset >= text.Length) Fail();
            switch (text[offset])
            {
                case '{': Container(depth + 1, true); return;
                case '[': Container(depth + 1, false); return;
                case '"': String(); return;
                case 't': Literal("true"); return;
                case 'f': Literal("false"); return;
                case 'n': Literal("null"); return;
                default: Number(); return;
            }
        }

        private void Container(int depth, bool isObject)
        {
            if (depth > maxDepth) throw new FormatException("JSON nesting exceeds the supported depth.");
            offset++;
            Whitespace();
            var close = isObject ? '}' : ']';
            if (Take(close)) return;
            while (true)
            {
                if (isObject)
                {
                    String();
                    Whitespace();
                    if (!Take(':')) Fail();
                }
                Value(depth);
                Whitespace();
                if (Take(close)) return;
                if (!Take(',')) Fail();
                Whitespace();
                // The next iteration requires a property/value, rejecting trailing commas.
            }
        }

        private void String()
        {
            if (!Take('"')) Fail();
            while (offset < text.Length)
            {
                var character = text[offset++];
                if (character == '"') return;
                if (character < 0x20) Fail();
                if (character != '\\') continue;
                if (offset >= text.Length) Fail();
                character = text[offset++];
                if (character == 'u')
                {
                    var codeUnit = UnicodeEscape();
                    if (char.IsHighSurrogate(codeUnit))
                    {
                        if (!Take('\\') || !Take('u') || !char.IsLowSurrogate(UnicodeEscape())) Fail();
                    }
                    else if (char.IsLowSurrogate(codeUnit)) Fail();
                }
                else if (character != '"' && character != '\\' && character != '/' &&
                         character != 'b' && character != 'f' && character != 'n' &&
                         character != 'r' && character != 't') Fail();
            }
            Fail();
        }

        private char UnicodeEscape()
        {
            var value = 0;
            for (var i = 0; i < 4; i++)
            {
                if (offset >= text.Length || !IsHex(text[offset])) Fail();
                var digit = text[offset++];
                value = value * 16 + (IsDigit(digit) ? digit - '0' : char.ToUpperInvariant(digit) - 'A' + 10);
            }
            return (char)value;
        }

        private void Number()
        {
            Take('-');
            if (!Take('0'))
            {
                if (offset >= text.Length || text[offset] < '1' || text[offset] > '9') Fail();
                Digits();
            }
            if (Take('.'))
            {
                if (offset >= text.Length || !IsDigit(text[offset])) Fail();
                Digits();
            }
            if (Take('e') || Take('E'))
            {
                if (!Take('+')) Take('-');
                if (offset >= text.Length || !IsDigit(text[offset])) Fail();
                Digits();
            }
        }

        private void Literal(string literal)
        {
            foreach (var character in literal) if (!Take(character)) Fail();
        }

        private void Digits() { while (offset < text.Length && IsDigit(text[offset])) offset++; }
        private static bool IsDigit(char value) => value >= '0' && value <= '9';
        private static bool IsHex(char value) => IsDigit(value) ||
            (value >= 'a' && value <= 'f') || (value >= 'A' && value <= 'F');
        private bool Take(char value)
        {
            if (offset >= text.Length || text[offset] != value) return false;
            offset++;
            return true;
        }

        private void Whitespace()
        {
            while (offset < text.Length && (text[offset] == ' ' || text[offset] == '\t' ||
                text[offset] == '\r' || text[offset] == '\n')) offset++;
        }

        private void Fail() => throw new FormatException("Invalid JSON syntax at character " + offset + ".");
    }
}
