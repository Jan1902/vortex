using Microsoft.Extensions.Logging;
using Vortex.Data;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Tasks.Blocks;
using Vortex.Modules.Behaviour.Tasks.Container;
using Vortex.Modules.Behaviour.Tasks.Helper;
using Vortex.Modules.Crafting.Abstraction;
using Vortex.Modules.Inventory.Abstraction;
using Vortex.Modules.Player.Abstraction;
using Vortex.Modules.World.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Tasks.Items;

/// <summary>
/// Crafts an item until the bot carries a number of it.
/// </summary>
/// <remarks>
/// <para>
/// Crafting is all this does. What goes into the item it asks for as items to
/// carry, and where those come from -- a chest, another recipe, a tree -- is up
/// to <see cref="ObtainItemsTask"/> and the sources behind it. The recipe is
/// chosen afresh each round from what the bot carries, so a switch to another
/// wood halfway through needs no plan to be thrown away.
/// </para>
/// <para>
/// A recipe too big for the inventory's 2 by 2 grid needs a crafting table: one
/// close by is used, otherwise one is carried and put down. The table is seen
/// to before the ingredients, because making one uses up planks that would
/// otherwise have to be fetched twice.
/// </para>
/// </remarks>
public class CraftTask(
    Item item,
    int count,
    ObtainChain chain,
    IInventoryManager inventory,
    ICraftingManager crafting,
    IWorldManager world,
    IPlayerManager player,
    Func<ItemRequest, ObtainChain, ObtainItemsTask> obtain,
    Func<Vector3i, OpenContainerTask> open,
    Func<Item, Vector3i, PlaceBlockTask> place,
    ILogger<CraftTask> logger) : BotTask
{
    /// <summary>How far around the bot a crafting table is looked for.</summary>
    private const int TableSearchRadius = 6;

    /// <summary>Where a carried crafting table may be put, relative to the bot's feet.</summary>
    private static readonly Vector3i[] TableSpots =
        [new(2, 0, 0), new(-2, 0, 0), new(0, 0, 2), new(0, 0, -2), new(2, 0, 2), new(-2, 0, -2), new(2, 0, -2), new(-2, 0, 2)];

    public override string Description
        => $"craft {item} until carrying {count}";

    public override bool IsSatisfied()
        => inventory.Count(item) >= count;

    public override IEnumerable<BotTask> Dependencies()
    {
        // No recipe to follow: the action below says why.
        if (CraftingPlanner.Choose(item, inventory.Count, chain) is not { } recipe)
            yield break;

        var making = chain.With([item]);
        var atTable = NeedsTable(recipe) && crafting.ActiveGrid is not { Size: 3 };
        var table = atTable ? FindTable() : null;

        if (atTable && table is null)
            yield return obtain(ItemRequest.Of(Item.CraftingTable, 1), making);

        // Enough for every craft still to go, so that the ingredients are
        // fetched in one go rather than once per craft.
        var crafts = (count - inventory.Count(item) + recipe.Result!.Count - 1) / recipe.Result.Count;

        foreach (var (ingredient, perCraft) in CraftingPlanner.Needs(recipe))
            yield return obtain(new ItemRequest(ingredient.Items, perCraft * Math.Max(crafts, 1)), making);

        if (!atTable)
            yield break;

        if (table is not null)
            yield return open(table);
        else if (FindSpotForTable() is { } spot)
            yield return place(Item.CraftingTable, spot);
    }

    public override async Task<TaskResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        if (CraftingPlanner.Choose(item, inventory.Count, chain) is not { } recipe)
            return TaskResult.Failed(CraftingPlanner.Explain(item));

        // A chest or furnace open on top has no grid to craft in.
        if (crafting.ActiveGrid is null)
            await inventory.CloseContainerAsync();

        if (crafting.ActiveGrid is not { } grid || !crafting.Fits(recipe, grid))
            return TaskResult.Failed($"{item} needs a crafting table");

        logger.LogDebug("Crafting {Recipe} in a {Size}x{Size} grid", recipe.Identifier, grid.Size, grid.Size);

        return await crafting.CraftAsync(recipe, cancellationToken)
            ? TaskResult.Success()
            : TaskResult.Failed($"crafting {recipe.Identifier} did not work");
    }

    private bool NeedsTable(Recipe recipe)
        => !crafting.Fits(recipe, new CraftingGrid(ContainerWindow.PlayerWindowId, 2));

    /// <summary>The nearest crafting table around the bot, if there is one.</summary>
    private Vector3i? FindTable()
    {
        var feet = player.Position.ToBlockPosition();
        Vector3i? nearest = null;
        var nearestDistance = int.MaxValue;

        for (var x = -TableSearchRadius; x <= TableSearchRadius; x++)
            for (var y = -TableSearchRadius; y <= TableSearchRadius; y++)
                for (var z = -TableSearchRadius; z <= TableSearchRadius; z++)
                {
                    var position = feet + new Vector3i(x, y, z);

                    if (world.GetBlock(position)?.Block != Block.CraftingTable)
                        continue;

                    var distance = x * x + y * y + z * z;
                    if (distance < nearestDistance)
                        (nearest, nearestDistance) = (position, distance);
                }

        return nearest;
    }

    /// <summary>An empty block next to the bot, on solid ground, to put a crafting table in.</summary>
    private Vector3i? FindSpotForTable()
    {
        var feet = player.Position.ToBlockPosition();

        foreach (var offset in TableSpots)
        {
            var spot = feet + offset;

            if (world.GetBlock(spot)?.Block is Block.Air or Block.CaveAir
                && BlockCollision.IsSolid(world.GetBlock(spot + new Vector3i(0, -1, 0))))
                return spot;
        }

        return null;
    }
}
