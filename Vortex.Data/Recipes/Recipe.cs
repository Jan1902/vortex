namespace Vortex.Data;

/// <summary>
/// The item a recipe produces, and how many of it.
/// </summary>
public sealed record RecipeResult(Item Item, int Count = 1);

/// <summary>
/// A recipe of the game.
/// </summary>
/// <param name="Identifier">
/// The name the server knows the recipe by, such as <c>minecraft:stick</c>, as
/// sent in recipe book packets.
/// </param>
public abstract record Recipe(string Identifier)
{
    /// <summary>Which station makes it.</summary>
    public abstract RecipeType Type { get; }

    /// <summary>What it makes, or <c>null</c> when that depends on the inputs, as for dyeing armour.</summary>
    public abstract RecipeResult? Result { get; }
}

/// <summary>
/// A crafting recipe whose ingredients have to be laid out in a particular shape.
/// </summary>
/// <param name="Pattern">
/// The rows of the shape, one character per slot, each standing for the
/// ingredient in <paramref name="Key"/>; a space leaves the slot empty.
/// </param>
/// <param name="Group">Recipes with the same group share one entry in the recipe book.</param>
public sealed record ShapedRecipe(
    string Identifier,
    RecipeCategory Category,
    string? Group,
    string[] Pattern,
    IReadOnlyDictionary<char, Ingredient> Key,
    RecipeResult Output) : Recipe(Identifier)
{
    public override RecipeType Type => RecipeType.Crafting;

    public override RecipeResult Result => Output;

    /// <summary>How many slots wide the shape is.</summary>
    public int Width => Pattern.Max(row => row.Length);

    /// <summary>How many slots tall the shape is.</summary>
    public int Height => Pattern.Length;

    /// <summary>
    /// The ingredient in one slot of the shape, counted from the top left.
    /// </summary>
    /// <returns>The ingredient, or <c>null</c> for a slot left empty.</returns>
    public Ingredient? At(int x, int y)
        => y < Pattern.Length && x < Pattern[y].Length && Key.TryGetValue(Pattern[y][x], out var ingredient) ? ingredient : null;
}

/// <summary>
/// A crafting recipe that takes its ingredients in any arrangement.
/// </summary>
public sealed record ShapelessRecipe(
    string Identifier,
    RecipeCategory Category,
    string? Group,
    Ingredient[] Ingredients,
    RecipeResult Output) : Recipe(Identifier)
{
    public override RecipeType Type => RecipeType.Crafting;

    public override RecipeResult Result => Output;
}

/// <summary>
/// A recipe that turns one item into another over time: in a furnace, blast
/// furnace, smoker or on a campfire.
/// </summary>
/// <param name="Station">Which of them; see <see cref="Recipe.Type"/>.</param>
/// <param name="CookingTime">How long it takes, in ticks.</param>
/// <param name="Experience">The experience it gives.</param>
public sealed record CookingRecipe(
    string Identifier,
    RecipeType Station,
    RecipeCategory Category,
    string? Group,
    Ingredient Ingredient,
    RecipeResult Output,
    int CookingTime,
    double Experience) : Recipe(Identifier)
{
    public override RecipeType Type => Station;

    public override RecipeResult Result => Output;
}

/// <summary>
/// A stonecutter recipe.
/// </summary>
public sealed record StonecuttingRecipe(
    string Identifier,
    Ingredient Ingredient,
    RecipeResult Output) : Recipe(Identifier)
{
    public override RecipeType Type => RecipeType.Stonecutting;

    public override RecipeResult Result => Output;
}

/// <summary>
/// A smithing table recipe that upgrades an item into another, such as diamond
/// gear into netherite.
/// </summary>
public sealed record SmithingTransformRecipe(
    string Identifier,
    Ingredient Template,
    Ingredient Base,
    Ingredient Addition,
    RecipeResult Output) : Recipe(Identifier)
{
    public override RecipeType Type => RecipeType.Smithing;

    public override RecipeResult Result => Output;
}

/// <summary>
/// A smithing table recipe that puts a trim on armour. The armour keeps its type,
/// so there is no fixed result.
/// </summary>
public sealed record SmithingTrimRecipe(
    string Identifier,
    Ingredient Template,
    Ingredient Base,
    Ingredient Addition) : Recipe(Identifier)
{
    public override RecipeType Type => RecipeType.Smithing;

    public override RecipeResult? Result => null;
}

/// <summary>
/// A crafting recipe whose inputs and result are worked out by the game's code,
/// such as dyeing armour or copying a map, and so not described by data.
/// </summary>
/// <param name="Serializer">Which of them it is.</param>
public sealed record SpecialRecipe(
    string Identifier,
    RecipeCategory Category,
    RecipeSerializer Serializer) : Recipe(Identifier)
{
    public override RecipeType Type => RecipeType.Crafting;

    public override RecipeResult? Result => null;
}
