using Vortex.Data;

namespace Vortex.Modules.Behaviour.Tasks;

/// <summary>
/// Decides how to craft an item from what the bot carries: which recipe, and
/// which ingredients have to be crafted first.
/// </summary>
/// <remarks>
/// Pure: it looks at counts it is given and the game's recipes, and remembers
/// nothing, so it can be asked again every round.
/// </remarks>
internal static class CraftingPlanner
{
    /// <summary>How many steps of crafting-to-craft are looked through: logs to planks to sticks is two.</summary>
    private const int MaxDepth = 5;

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
    /// hand if there is one, otherwise one whose missing ingredients can be
    /// crafted from what is at hand.
    /// </summary>
    /// <param name="have">How many of an item the bot carries.</param>
    /// <returns>The recipe, or <c>null</c> if nothing at hand leads to the item.</returns>
    public static Recipe? Choose(Item item, Func<Item, int> have)
    {
        var recipes = CraftingRecipes(item).ToList();

        return recipes.FirstOrDefault(recipe => Needs(recipe).All(need => Available(need.Ingredient, have) >= need.Count))
            ?? recipes.FirstOrDefault(recipe => CanMake(recipe, have, [item], 0));
    }

    /// <summary>
    /// For an ingredient that is short, the item to craft to cover it: one that
    /// can be made from what is at hand.
    /// </summary>
    public static Item? ItemToCraft(Ingredient ingredient, Func<Item, int> have)
        => ingredient.Items
            .OrderBy(item => item)
            .Where(item => CraftingRecipes(item).Any(recipe => CanMake(recipe, have, [item], 1)))
            .Select(item => (Item?)item)
            .FirstOrDefault();

    /// <summary>
    /// Why an item cannot be made: the first ingredient of its first recipe that
    /// is neither at hand nor craftable.
    /// </summary>
    public static string Explain(Item item, Func<Item, int> have)
    {
        if (CraftingRecipes(item).FirstOrDefault() is not { } recipe)
            return $"{item} cannot be crafted";

        var missing = Needs(recipe).FirstOrDefault(need =>
            Available(need.Ingredient, have) < need.Count && ItemToCraft(need.Ingredient, have) is null);

        return missing.Ingredient is { } ingredient
            ? $"{item} needs {missing.Count} {ingredient}, and the bot has none and cannot make any"
            : $"{item} cannot be made from what the bot carries";
    }

    /// <summary>How many of the items that satisfy an ingredient the bot carries in all.</summary>
    public static int Available(Ingredient ingredient, Func<Item, int> have)
        => ingredient.Items.Sum(have);

    private static bool CanMake(Recipe recipe, Func<Item, int> have, HashSet<Item> making, int depth)
    {
        if (depth > MaxDepth)
            return false;

        foreach (var (ingredient, count) in Needs(recipe))
        {
            if (Available(ingredient, have) >= count)
                continue;

            // Anything already being made further up would go round in circles,
            // as iron ingots and iron blocks make each other.
            var craftable = ingredient.Items
                .Where(item => !making.Contains(item))
                .Any(item => CraftingRecipes(item).Any(inner => CanMake(inner, have, [.. making, item], depth + 1)));

            if (!craftable)
                return false;
        }

        return true;
    }
}
