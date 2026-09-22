using Microsoft.Extensions.Logging;
using Vortex.Data;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Crafting.Abstraction;
using Vortex.Modules.Inventory.Abstraction;
using Vortex.Modules.Player.Abstraction;
using Vortex.Modules.World.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Tasks;

/// <summary>
/// Crafts an item until the bot carries a number of it, crafting what goes into
/// it first where that is missing.
/// </summary>
/// <remarks>
/// <para>
/// Each round the recipe is chosen afresh from what the bot carries, so sticks
/// asked for with only logs at hand become planks first and sticks after,
/// without a plan to follow or discard.
/// </para>
/// <para>
/// A recipe too big for the inventory's 2 by 2 grid needs a crafting table: one
/// close by is used, one carried is placed, and failing both one is crafted.
/// Raw materials are not gathered; lacking them, the task says what is missing.
/// </para>
/// </remarks>
public class CraftTask(
    Item item,
    int count,
    IInventoryManager inventory,
    ICraftingManager crafting,
    IWorldManager world,
    IPlayerManager player,
    Func<Item, int, CraftTask> craft,
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
        => $"carry {count} {item}";

    public override bool IsSatisfied()
        => inventory.Count(item) >= count;

    public override IEnumerable<BotTask> Dependencies()
    {
        // No recipe to follow: the action below says why.
        if (CraftingPlanner.Choose(item, inventory.Count) is not { } recipe)
            yield break;

        foreach (var (ingredient, needed) in CraftingPlanner.Needs(recipe))
        {
            var available = CraftingPlanner.Available(ingredient, inventory.Count);
            if (available >= needed)
                continue;

            if (CraftingPlanner.ItemToCraft(ingredient, inventory.Count) is not { } part)
                yield break;

            yield return craft(part, inventory.Count(part) + needed - available);
        }

        if (!NeedsTable(recipe) || crafting.ActiveGrid is { Size: 3 })
            yield break;

        if (FindTable() is { } table)
        {
            yield return open(table);
            yield break;
        }

        if (inventory.Count(Item.CraftingTable) > 0 && FindSpotForTable() is { } spot)
        {
            yield return place(Item.CraftingTable, spot);
            yield break;
        }

        yield return craft(Item.CraftingTable, 1);
    }

    public override async Task<TaskResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        if (CraftingPlanner.Choose(item, inventory.Count) is not { } recipe)
            return TaskResult.Failed(CraftingPlanner.Explain(item, inventory.Count));

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
