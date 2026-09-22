using Microsoft.Extensions.Logging;
using Vortex.Data;
using Vortex.Modules.Crafting.Abstraction;
using Vortex.Modules.Inventory.Abstraction;
using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Crafting;

/// <summary>
/// Crafts by clicking: one of each ingredient into the grid, then the result out.
/// </summary>
/// <remarks>
/// Laying out by clicks rather than through the recipe book works for every
/// recipe, unlocked or not, which the recipe book does not.
/// </remarks>
internal class CraftingManager(
    INetworkingManager networking,
    IInventoryManager inventory,
    ILogger<CraftingManager> logger) : ICraftingManager
{
    private readonly HashSet<string> _unlocked = [];
    private readonly object _lock = new();

    public IReadOnlySet<string> UnlockedRecipes
    {
        get
        {
            lock (_lock)
                return _unlocked.ToHashSet();
        }
    }

    public CraftingGrid? ActiveGrid
        => inventory.OpenContainer switch
        {
            null => new CraftingGrid(ContainerWindow.PlayerWindowId, 2),
            { Type: Menu.Crafting } table => new CraftingGrid(table.Id, 3),
            _ => null,
        };

    public bool Fits(Recipe recipe, CraftingGrid grid)
        => recipe switch
        {
            ShapedRecipe shaped => shaped.Width <= grid.Size && shaped.Height <= grid.Size,
            ShapelessRecipe shapeless => shapeless.Ingredients.Length <= grid.Size * grid.Size,
            _ => false,
        };

    public async Task<bool> CraftAsync(Recipe recipe, CancellationToken cancellationToken = default)
    {
        if (ActiveGrid is not { } grid || !Fits(recipe, grid) || recipe.Result is not { } result)
            return false;

        await EmptyCursor();
        await ClearGrid(grid);

        foreach (var (slot, ingredient) in Layout(recipe, grid))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!await PlaceOne(ingredient, slot))
            {
                logger.LogDebug("Missing an ingredient for {Recipe}", recipe.Identifier);
                await ClearGrid(grid);

                return false;
            }
        }

        if (inventory.ActiveWindow.Slots[CraftingGrid.ResultSlot]?.Item != result.Item)
        {
            logger.LogDebug("The grid did not produce {Item} for {Recipe}", result.Item, recipe.Identifier);
            await ClearGrid(grid);

            return false;
        }

        var before = inventory.Count(result.Item);

        // With one of each ingredient in the grid, shift-clicking the result
        // crafts exactly once and moves it into the inventory.
        await inventory.QuickMoveAsync(CraftingGrid.ResultSlot);

        return inventory.Count(result.Item) > before;
    }

    public async Task PlaceRecipeAsync(Recipe recipe, bool makeAll = false)
    {
        if (ActiveGrid is { } grid)
            await networking.SendPacket(new PlaceRecipe((byte)grid.WindowId, recipe.Identifier, makeAll));
    }

    public void UpdateRecipeBook(RecipeBookAction action, IEnumerable<string> recipes)
    {
        lock (_lock)
        {
            switch (action)
            {
                case RecipeBookAction.Init:
                    _unlocked.Clear();
                    _unlocked.UnionWith(recipes);
                    break;
                case RecipeBookAction.Add:
                    _unlocked.UnionWith(recipes);
                    break;
                case RecipeBookAction.Remove:
                    _unlocked.ExceptWith(recipes);
                    break;
            }
        }
    }

    /// <summary>
    /// Where each ingredient goes: a shaped recipe's pattern from the top left
    /// corner, a shapeless recipe's ingredients one after the other.
    /// </summary>
    private static IEnumerable<(int Slot, Ingredient Ingredient)> Layout(Recipe recipe, CraftingGrid grid)
    {
        if (recipe is ShapedRecipe shaped)
        {
            for (var y = 0; y < shaped.Height; y++)
                for (var x = 0; x < shaped.Width; x++)
                    if (shaped.At(x, y) is { } ingredient)
                        yield return (grid.Slot(x, y), ingredient);
        }
        else if (recipe is ShapelessRecipe shapeless)
        {
            for (var i = 0; i < shapeless.Ingredients.Length; i++)
                yield return (1 + i, shapeless.Ingredients[i]);
        }
    }

    /// <summary>
    /// Puts one item that satisfies the ingredient into a grid slot: picks up a
    /// stack, right-clicks one off it into the slot, and puts the rest back.
    /// </summary>
    private async Task<bool> PlaceOne(Ingredient ingredient, int gridSlot)
    {
        var source = inventory.Find(stack => ingredient.Matches(stack.Item) && !stack.HasComponents)
            .Select(found => inventory.ToActiveWindowSlot(found.Slot))
            .FirstOrDefault(slot => slot is not null);

        if (source is not { } from)
            return false;

        await inventory.PickUpAsync(from);
        await inventory.ClickAsync(gridSlot, 1, ClickMode.PickUp);
        await inventory.PickUpAsync(from);

        return inventory.ActiveWindow.Slots[gridSlot] is not null;
    }

    /// <summary>Moves whatever is left in the grid back into the inventory.</summary>
    private async Task ClearGrid(CraftingGrid grid)
    {
        foreach (var slot in grid.Slots)
            if (inventory.ActiveWindow.Slots[slot] is not null)
                await inventory.QuickMoveAsync(slot);
    }

    /// <summary>Puts down whatever an earlier click left on the cursor, into an empty slot.</summary>
    private async Task EmptyCursor()
    {
        if (inventory.Cursor is null)
            return;

        var empty = PlayerSlots.Storage
            .Where(slot => slot != PlayerSlots.Offhand && inventory.Player.Slots[slot] is null)
            .Select(slot => inventory.ToActiveWindowSlot(slot))
            .FirstOrDefault(slot => slot is not null);

        if (empty is { } slot)
            await inventory.PickUpAsync(slot);
    }
}
