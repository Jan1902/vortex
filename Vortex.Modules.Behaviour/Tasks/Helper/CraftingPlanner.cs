using Vortex.Data;
using Vortex.Modules.Behaviour.Abstraction;

namespace Vortex.Modules.Behaviour.Tasks.Helper;

/// <summary>
/// Decides how to craft an item: which recipe, and what it takes.
/// </summary>
/// <remarks>
/// Pure: it looks at counts it is given and the game's recipes, and remembers
/// nothing, so it can be asked again every round.
/// </remarks>
internal static class CraftingPlanner
{
    /// <summary>The crafting recipes that make an item; cooking, smithing and the like are not crafting.</summary>
    public static IEnumerable<Recipe> CraftingRecipes(Item item)
        => Recipes.Producing(item).Where(recipe => recipe is ShapedRecipe or ShapelessRecipe);

    /// <summary>
    /// What one craft of a recipe uses: each distinct ingredient and how many of it.
    /// </summary>
    public static List<(Ingredient Ingredient, int Count)> Needs(Recipe recipe)
    {
        IEnumerable<Ingredient> ingredients = recipe switch
        {
            ShapedRecipe shaped => shaped.Pattern.SelectMany(row => row).Where(shaped.Key.ContainsKey).Select(symbol => shaped.Key[symbol]),
            ShapelessRecipe shapeless => shapeless.Ingredients,
            _ => [],
        };

        var needs = new List<(Ingredient Ingredient, int Count)>();

        // Two slots asking for the same items are one need, counted twice.
        foreach (var ingredient in ingredients)
        {
            var index = needs.FindIndex(need => need.Ingredient.Items.SetEquals(ingredient.Items));

            if (index < 0)
                needs.Add((ingredient, 1));
            else
                needs[index] = (needs[index].Ingredient, needs[index].Count + 1);
        }

        return needs;
    }

    /// <summary>
    /// Picks the recipe to make an item with: one whose ingredients are all at
    /// hand if there is one, otherwise the first that does not go round in
    /// circles.
    /// </summary>
    /// <remarks>
    /// Whether the missing ingredients can actually be had is not for crafting
    /// to know -- they may come from a chest, a tree or another recipe. The
    /// task asks for them and finds out.
    /// </remarks>
    /// <param name="have">How many of an item the bot carries.</param>
    /// <param name="chain">What is being obtained further up, which no ingredient may be made of alone.</param>
    /// <returns>The recipe, or <c>null</c> if the item has no recipe that could work.</returns>
    public static Recipe? Choose(Item item, Func<Item, int> have, ObtainChain chain)
    {
        var usable = CraftingRecipes(item)
            .Where(recipe => Needs(recipe).All(need => need.Ingredient.Items.Any(candidate => candidate != item && !chain.Contains(candidate))))
            .ToList();

        return usable.FirstOrDefault(recipe => Needs(recipe).All(need => Available(need.Ingredient, have) >= need.Count))
            ?? usable.FirstOrDefault();
    }

    /// <summary>Why an item cannot be crafted at all.</summary>
    public static string Explain(Item item)
        => CraftingRecipes(item).Any()
            ? $"every recipe for {item} needs what is being made from it"
            : $"{item} cannot be crafted";

    /// <summary>How many of the items that satisfy an ingredient the bot carries in all.</summary>
    public static int Available(Ingredient ingredient, Func<Item, int> have)
        => ingredient.Items.Sum(have);
}
