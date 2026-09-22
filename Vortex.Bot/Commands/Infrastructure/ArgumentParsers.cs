using System.Globalization;
using Vortex.Data;
using Vortex.Shared;

namespace Vortex.Bot.Commands.Infrastructure;

/// <summary>
/// Turns the words of a command into argument values, by the argument's type.
/// </summary>
public static class ArgumentParsers
{
    /// <summary>
    /// Reads a value from the words starting at an index.
    /// </summary>
    /// <returns>The value and how many words it took, or <c>null</c> if the words do not make one.</returns>
    public delegate (object Value, int Words)? Parser(IReadOnlyList<string> words, int index);

    private static readonly Dictionary<Type, (Parser Parse, string Usage)> _parsers = new()
    {
        [typeof(string)] = (One(word => word), "{0}"),
        [typeof(int)] = (One(word => int.TryParse(word, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null), "{0}"),
        [typeof(double)] = (One(word => TryParseNumber(word, out var value) ? value : null), "{0}"),
        [typeof(Item)] = (One(word => ParseName<Item>(word)), "{0}"),
        [typeof(EntityType)] = (One(word => ParseName<EntityType>(word)), "{0}"),
        [typeof(Vector3i)] = (ParseBlock, "x y z"),
    };

    /// <summary>Whether there is a parser for a type.</summary>
    public static bool Supports(Type type)
        => _parsers.ContainsKey(type);

    /// <summary>Reads a value of a type from the words starting at an index.</summary>
    public static (object Value, int Words)? Parse(Type type, IReadOnlyList<string> words, int index)
        => index < words.Count ? _parsers[type].Parse(words, index) : null;

    /// <summary>How an argument is written in a usage line, such as <c>&lt;x y z&gt;</c>.</summary>
    public static string Usage(Type type, string name)
        => string.Format(CultureInfo.InvariantCulture, _parsers[type].Usage, name);

    /// <summary>
    /// Reads one number, written with either separator.
    /// </summary>
    /// <remarks>
    /// The game writes coordinates with a dot wherever it runs, so parsing is
    /// invariant rather than following the machine's locale. A comma counts as
    /// the same, because someone on a German keyboard types one, and the words
    /// of a command are separated by spaces, so it cannot mean anything else.
    /// </remarks>
    public static bool TryParseNumber(string word, out double value)
        => double.TryParse(word.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    /// <summary>
    /// Reads a block position from three numbers.
    /// </summary>
    /// <remarks>
    /// A whole number is a block coordinate and stands for itself. A number with
    /// a fraction -- what the F3 screen shows, and what the bot reports as its
    /// exact position -- names a point, and the block meant is the one containing
    /// it. Both go through the same rule, so 42.5 and 42 are both block 42, and
    /// -16.5 and -17 are both block -17.
    /// </remarks>
    private static (object Value, int Words)? ParseBlock(IReadOnlyList<string> words, int index)
    {
        if (index + 3 > words.Count)
            return null;

        var coordinates = new double[3];

        for (var i = 0; i < 3; i++)
            if (!TryParseNumber(words[index + i], out coordinates[i]))
                return null;

        return (new Vector3d(coordinates[0], coordinates[1], coordinates[2]).ToBlockPosition(), 3);
    }

    /// <summary>
    /// Reads a name as the game writes it -- <c>oak_log</c>, or
    /// <c>minecraft:oak_log</c> -- as the generated enum value.
    /// </summary>
    private static TEnum? ParseName<TEnum>(string word) where TEnum : struct, Enum
    {
        var name = string.Concat(word.Replace("minecraft:", "", StringComparison.OrdinalIgnoreCase)
            .Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));

        // Enum.TryParse takes numbers as well, which would make a coordinate
        // typed in the wrong place into some item.
        return name.Length > 0 && char.IsLetter(name[0]) && Enum.TryParse<TEnum>(name, ignoreCase: true, out var value) ? value : null;
    }

    /// <summary>A parser for a value written as a single word; <paramref name="parse"/> returns <c>null</c> for a word that is not one.</summary>
    private static Parser One(Func<string, object?> parse)
        => (words, index) => parse(words[index]) is { } value ? (value, 1) : null;
}
