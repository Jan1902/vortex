using Vortex.Data;

namespace Vortex.Modules.Crafting.Abstraction;

/// <summary>
/// Crafts in whichever crafting grid is open, and knows the recipe book.
/// </summary>
/// <remarks>
/// One recipe at a time, with what the bot already carries. Working out what
/// to craft first, getting the ingredients and opening a crafting table are
/// left to whoever calls.
/// </remarks>
public interface ICraftingManager
{
    /// <summary>The recipes the player has unlocked, by identifier, as the recipe book shows them.</summary>
    IReadOnlySet<string> UnlockedRecipes { get; }

    /// <summary>
    /// The crafting grid of the open window: the 2 by 2 one of the inventory, or
    /// the 3 by 3 one of a crafting table; <c>null</c> when another container is
    /// open.
    /// </summary>
    CraftingGrid? ActiveGrid { get; }

    /// <summary>Whether a recipe fits the grid, by its size.</summary>
    bool Fits(Recipe recipe, CraftingGrid grid);

    /// <summary>
    /// Crafts a recipe once in <see cref="ActiveGrid"/>: lays out one of each
    /// ingredient from the inventory and takes the result.
    /// </summary>
    /// <returns>
    /// Whether the result arrived in the inventory. Fails without crafting when
    /// no grid is open, the recipe does not fit it, or an ingredient is missing.
    /// </returns>
    Task<bool> CraftAsync(Recipe recipe, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks the server to lay out a recipe from the recipe book, as clicking it
    /// in the book does. The server only does so for unlocked recipes.
    /// </summary>
    /// <param name="makeAll">Lays out as many as the ingredients allow, as shift-clicking in the book does.</param>
    Task PlaceRecipeAsync(Recipe recipe, bool makeAll = false);
}

/// <summary>
/// A crafting grid in a window.
/// </summary>
/// <param name="WindowId">The window the grid is part of.</param>
/// <param name="Size">How many slots wide and high it is: 2 or 3.</param>
public sealed record CraftingGrid(int WindowId, int Size)
{
    /// <summary>The slot the result appears in.</summary>
    public const int ResultSlot = 0;

    /// <summary>The window slot of a grid position, counted from the top left.</summary>
    public int Slot(int x, int y) => 1 + y * Size + x;

    /// <summary>Every slot of the grid.</summary>
    public IEnumerable<int> Slots => Enumerable.Range(1, Size * Size);
}
