using System.Collections.Frozen;

namespace Vortex.Data;

/// <summary>
/// What a recipe accepts in one place: a single item, any item of a tag, or any
/// of several alternatives.
/// </summary>
public sealed class Ingredient
{
    private Ingredient(IReadOnlySet<Item> items) => Items = items;

    /// <summary>Every item that satisfies the ingredient.</summary>
    public IReadOnlySet<Item> Items { get; }

    /// <summary>An ingredient satisfied by exactly one item.</summary>
    public static Ingredient Of(Item item)
        => new(new[] { item }.ToFrozenSet());

    /// <summary>An ingredient satisfied by any item of a tag, such as <see cref="ItemTags.Planks"/>.</summary>
    public static Ingredient Of(IReadOnlySet<Item> tag)
        => new(tag);

    /// <summary>An ingredient satisfied by whatever satisfies any of the alternatives.</summary>
    public static Ingredient AnyOf(params Ingredient[] alternatives)
        => new(alternatives.SelectMany(alternative => alternative.Items).ToFrozenSet());

    /// <summary>Whether the item can be used for this ingredient.</summary>
    public bool Matches(Item item)
        => Items.Contains(item);

    public override string ToString()
        => Items.Count == 1 ? Items.First().ToString() : $"any of {Items.Count} items";
}
