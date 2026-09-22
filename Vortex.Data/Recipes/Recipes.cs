using System.Collections.Frozen;

namespace Vortex.Data;

/// <summary>
/// Every recipe of the game. Each one is a property named after it, such as
/// <c>Recipes.Stick</c>, generated from Mojang's recipe files.
/// </summary>
public static partial class Recipes
{
    private static FrozenDictionary<Item, Recipe[]>? _byResult;

    /// <summary>
    /// The recipes that produce an item.
    /// </summary>
    public static IReadOnlyList<Recipe> Producing(Item item)
    {
        _byResult ??= All
            .Where(recipe => recipe.Result is not null)
            .GroupBy(recipe => recipe.Result!.Item)
            .ToFrozenDictionary(group => group.Key, group => group.ToArray());

        return _byResult.TryGetValue(item, out var recipes) ? recipes : [];
    }
}
