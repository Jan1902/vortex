using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Vortex.CodeGeneration;

/// <summary>
/// A minimal JSON reader for the data Mojang's data generator produces.
/// </summary>
/// <remarks>
/// A source generator referenced as a project does not get its NuGet dependencies
/// loaded alongside it, so a JSON library cannot be used here. Objects become a
/// <see cref="JsonObject"/>, arrays a <see cref="List{T}"/>, numbers a
/// <see cref="double"/>.
/// </remarks>
internal static class SimpleJson
{
    /// <summary>
    /// Parses a document whose root is an array.
    /// </summary>
    public static List<object?> ParseArray(string text)
    {
        var position = 0;

        return ParseValue(text, ref position) as List<object?>
            ?? throw new FormatException("Expected the document to be a JSON array");
    }

    public static JsonObject Parse(string text)
    {
        var position = 0;
        var value = ParseValue(text, ref position);

        return value as JsonObject
            ?? throw new FormatException("Expected the document to be a JSON object");
    }

    private static object? ParseValue(string text, ref int position)
    {
        SkipWhitespace(text, ref position);

        if (position >= text.Length)
            throw new FormatException("Unexpected end of JSON document");

        switch (text[position])
        {
            case '{':
                return ParseObject(text, ref position);
            case '[':
                return ParseArray(text, ref position);
            case '"':
                return ParseString(text, ref position);
            case 't':
                Expect(text, ref position, "true");
                return true;
            case 'f':
                Expect(text, ref position, "false");
                return false;
            case 'n':
                Expect(text, ref position, "null");
                return null;
            default:
                return ParseNumber(text, ref position);
        }
    }

    private static JsonObject ParseObject(string text, ref int position)
    {
        var result = new JsonObject();

        position++; // '{'
        SkipWhitespace(text, ref position);

        if (position < text.Length && text[position] == '}')
        {
            position++;
            return result;
        }

        while (true)
        {
            SkipWhitespace(text, ref position);

            var name = ParseString(text, ref position);

            SkipWhitespace(text, ref position);
            if (text[position] != ':')
                throw new FormatException($"Expected ':' at position {position}");
            position++;

            result[name] = ParseValue(text, ref position);

            SkipWhitespace(text, ref position);

            if (text[position] == ',')
            {
                position++;
                continue;
            }

            if (text[position] == '}')
            {
                position++;
                return result;
            }

            throw new FormatException($"Expected ',' or '}}' at position {position}");
        }
    }

    private static List<object?> ParseArray(string text, ref int position)
    {
        var result = new List<object?>();

        position++; // '['
        SkipWhitespace(text, ref position);

        if (position < text.Length && text[position] == ']')
        {
            position++;
            return result;
        }

        while (true)
        {
            result.Add(ParseValue(text, ref position));

            SkipWhitespace(text, ref position);

            if (text[position] == ',')
            {
                position++;
                continue;
            }

            if (text[position] == ']')
            {
                position++;
                return result;
            }

            throw new FormatException($"Expected ',' or ']' at position {position}");
        }
    }

    private static string ParseString(string text, ref int position)
    {
        if (text[position] != '"')
            throw new FormatException($"Expected a string at position {position}");

        position++; // '"'

        var builder = new StringBuilder();

        while (text[position] != '"')
        {
            if (text[position] == '\\')
            {
                position++;

                builder.Append(text[position] switch
                {
                    'n' => '\n',
                    't' => '\t',
                    'r' => '\r',
                    'b' => '\b',
                    'f' => '\f',
                    'u' => (char)int.Parse(text.Substring(position + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    var other => other
                });

                if (text[position] == 'u')
                    position += 4;

                position++;
                continue;
            }

            builder.Append(text[position]);
            position++;
        }

        position++; // closing '"'

        return builder.ToString();
    }

    private static double ParseNumber(string text, ref int position)
    {
        var start = position;

        while (position < text.Length && (char.IsDigit(text[position]) || "+-.eE".IndexOf(text[position]) >= 0))
            position++;

        return double.Parse(text.Substring(start, position - start), CultureInfo.InvariantCulture);
    }

    private static void Expect(string text, ref int position, string literal)
    {
        if (string.CompareOrdinal(text, position, literal, 0, literal.Length) != 0)
            throw new FormatException($"Expected '{literal}' at position {position}");

        position += literal.Length;
    }

    private static void SkipWhitespace(string text, ref int position)
    {
        while (position < text.Length && char.IsWhiteSpace(text[position]))
            position++;
    }
}

/// <summary>
/// A parsed JSON object, keyed by property name in document order.
/// </summary>
internal class JsonObject : Dictionary<string, object?>
{
    public JsonObject? GetObject(string name)
        => TryGetValue(name, out var value) ? value as JsonObject : null;

    public List<object?>? GetArray(string name)
        => TryGetValue(name, out var value) ? value as List<object?> : null;

    public string? GetString(string name)
        => TryGetValue(name, out var value) ? value as string : null;

    public double? GetDouble(string name)
        => TryGetValue(name, out var value) && value is double number ? number : null;

    public int? GetInt(string name)
        => GetDouble(name) is { } number ? (int)number : null;

    public bool? GetBool(string name)
        => TryGetValue(name, out var value) && value is bool flag ? flag : null;
}
