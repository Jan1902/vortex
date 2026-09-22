using System;
using System.Collections.Generic;
using System.Linq;
using Vortex.CodeGeneration;

namespace Vortex.Data.CodeGeneration;

internal enum PropertyType
{
    Bool,
    Int,
    Enum
}

/// <summary>
/// What a block state property holds.
/// </summary>
/// <param name="EnumValues">The names an enum property can take, in the order of the generated enum.</param>
internal sealed record PropertyKind(PropertyType Type, List<string> EnumValues)
{
    /// <summary>
    /// The number a value is stored as in the state table: 1 or 0 for a bool,
    /// the number itself, or the value's position in the generated enum.
    /// </summary>
    public string Encode(string value)
        => Type switch
        {
            PropertyType.Bool => value == "true" ? "1" : "0",
            PropertyType.Int => value,
            _ => EnumValues.IndexOf(value).ToString(),
        };

    /// <summary>
    /// The value as a C# expression of the type <c>BlockState.Get</c> returns for
    /// the property: <c>true</c>, <c>7</c> or <c>HalfValue.Upper</c>.
    /// </summary>
    public string ToExpression(string property, string value)
        => Type switch
        {
            PropertyType.Bool or PropertyType.Int => value,
            _ => $"{Naming.ToIdentifier(property)}Value.{Naming.ToIdentifier(value)}",
        };
}

/// <summary>
/// Works out for every block state property name whether it holds a bool, a
/// number or one of a set of names.
/// </summary>
/// <remarks>
/// The same name can have different values on different blocks -- a fence's
/// <c>east</c> is true or false, a wall's is none, low or tall -- so the names
/// of an enum property are the union over all blocks.
/// </remarks>
internal static class BlockPropertyKinds
{
    /// <param name="blocksReport">Mojang's blocks report.</param>
    public static SortedDictionary<string, PropertyKind> Classify(JsonObject blocksReport)
    {
        var values = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        foreach (var block in blocksReport.Values.OfType<JsonObject>())
        {
            foreach (var property in block.GetObject("properties") ?? new JsonObject())
            {
                if (!values.TryGetValue(property.Key, out var set))
                    values[property.Key] = set = new SortedSet<string>(StringComparer.Ordinal);

                set.UnionWith(((List<object?>)property.Value!).Cast<string>());
            }
        }

        var kinds = new SortedDictionary<string, PropertyKind>(StringComparer.Ordinal);

        foreach (var property in values)
        {
            if (property.Value.All(value => value is "true" or "false"))
                kinds[property.Key] = new PropertyKind(PropertyType.Bool, []);
            else if (property.Value.All(value => value.All(char.IsDigit)))
                kinds[property.Key] = new PropertyKind(PropertyType.Int, []);
            else
                kinds[property.Key] = new PropertyKind(PropertyType.Enum, [.. property.Value]);
        }

        return kinds;
    }
}
