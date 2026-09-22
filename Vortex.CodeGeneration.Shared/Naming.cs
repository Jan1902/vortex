using System;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;

namespace Vortex.CodeGeneration;

/// <summary>
/// Turns Mojang's resource locations into C# names.
/// </summary>
internal static class Naming
{
    private static readonly char[] Separators = ['_', '/', '.', '-'];

    /// <summary>
    /// Converts a resource location such as <c>minecraft:worldgen/biome_source</c>
    /// into a PascalCase identifier such as <c>WorldgenBiomeSource</c>.
    /// </summary>
    public static string ToIdentifier(string resourceLocation)
    {
        var name = string.Concat(StripNamespace(resourceLocation)
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word.Substring(1).ToLowerInvariant()));

        // Identifiers cannot start with a digit, and a few Mojang names would.
        return SyntaxFacts.IsIdentifierStartCharacter(name[0]) ? name : "_" + name;
    }

    /// <summary>
    /// Removes the <c>minecraft:</c> part of a resource location.
    /// </summary>
    public static string StripNamespace(string resourceLocation)
    {
        var separator = resourceLocation.IndexOf(':');

        return separator < 0 ? resourceLocation : resourceLocation.Substring(separator + 1);
    }

    /// <summary>
    /// Escapes a string for use inside a C# string literal.
    /// </summary>
    public static string ToLiteral(string value)
        => SyntaxFactory.Literal(value).ToString();

    /// <summary>
    /// Writes a character as a C# character literal.
    /// </summary>
    public static string ToLiteral(char value)
        => SyntaxFactory.Literal(value).ToString();
}
