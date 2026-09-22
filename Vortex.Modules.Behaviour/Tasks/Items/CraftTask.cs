using Microsoft.Extensions.Logging;
using Vortex.Data;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Tasks.Blocks;
using Vortex.Modules.Behaviour.Tasks.Container;
using Vortex.Modules.Behaviour.Tasks.Helper;
using Vortex.Modules.Crafting.Abstraction;
using Vortex.Modules.Inventory.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Tasks.Items;

/// <summary>
/// Crafts an item until the bot carries a number of it.
/// </summary>
/// <remarks>
/// What goes into it is asked for with <see cref="GetItemsTask"/>, so the
/// ingredients may just as well come out of a chest or a tree. A recipe too big
/// for the inventory's 2 by 2 grid needs a crafting table: one close by is used,
/// otherwise one is fetched and put down. The table is seen to first, because
/// making one uses up planks that would otherwise be fetched twice.
/// </remarks>
public class CraftTask(Item item, int count) : BotTask
{
    /// <summary>How far around the bot a crafting table is looked for.</summary>
    private const int TableSearchRadius = 6;

    /// <summary>Where a carried crafting table may be put, relative to the bot's feet.</summary>
    private static readonly Vector3i[] TableSpots =
        [new(2, 0, 0), new(-2, 0, 0), new(0, 0, 2), new(0, 0, -2), new(2, 0, 2), new(-2, 0, -2), new(2, 0, -2), new(-2, 0, 2)];

    /// <summary>The item being crafted, so that nothing is made out of itself further down.</summary>
    public Item Item => item;

    public override string Description
        => $"craft {item} until carrying {count}";

    public override bool IsDone(Bot bot)
        => bot.Inventory.Count(item) >= count;

    public override async Task<TaskResult> RunAsync(Bot bot)
    {
        // One craft per round: what a craft leaves behind is what the next one
        // is worked out from.
        for (var round = 0; round < 64; round++)
        {
            if (IsDone(bot))
                return TaskResult.Success();

            if (CraftingPlanner.Choose(item, bot.Inventory.Count, GetItemsTask.BeingGot(bot)) is not { } recipe)
                return TaskResult.Failed($"no recipe for {item} that is not made of what is being fetched for it");

            var needsTable = !bot.Crafting.Fits(recipe, new CraftingGrid(ContainerWindow.PlayerWindowId, 2));
            var atTable = bot.Crafting.ActiveGrid is { Size: 3 };

            // A table to work at, before the ingredients: making one costs planks.
            if (needsTable && !atTable && FindTable(bot) is null && bot.Inventory.Count(Item.CraftingTable) == 0)
            {
                var got = await bot.Run(new GetItemsTask(Item.CraftingTable, 1));

                if (got.IsFailure)
                    return got;
            }

            // Enough for every craft still to go, so the ingredients are
            // fetched in one go rather than once per craft.
            var crafts = (count - bot.Inventory.Count(item) + recipe.Result!.Count - 1) / recipe.Result.Count;

            foreach (var (ingredient, perCraft) in CraftingPlanner.Needs(recipe))
            {
                var needed = perCraft * Math.Max(crafts, 1);

                if (CraftingPlanner.Available(ingredient, bot.Inventory.Count) >= needed)
                    continue;

                var got = await bot.Run(new GetItemsTask(ingredient.Items, needed));

                if (got.IsFailure)
                    return got;
            }

            // Fetching one ingredient can eat another: sticks are made of the
            // planks just fetched. Another round then fetches what is short.
            if (CraftingPlanner.Needs(recipe).Any(need => CraftingPlanner.Available(need.Ingredient, bot.Inventory.Count) < need.Count))
                continue;

            if (needsTable && bot.Crafting.ActiveGrid is not { Size: 3 })
            {
                var opened = await OpenTable(bot);

                if (opened.IsFailure)
                    return opened;
            }

            // A chest or furnace open on top has no grid to craft in.
            if (bot.Crafting.ActiveGrid is null)
                await bot.Inventory.CloseContainerAsync();

            if (bot.Crafting.ActiveGrid is not { } grid || !bot.Crafting.Fits(recipe, grid))
                return TaskResult.Failed($"{item} needs a crafting table");

            bot.Logger.LogDebug("Crafting {Recipe} in a {Size}x{Size} grid", recipe.Identifier, grid.Size, grid.Size);

            if (!await bot.Crafting.CraftAsync(recipe, bot.Cancellation))
                return TaskResult.Failed($"crafting {recipe.Identifier} did not work");
        }

        return TaskResult.Failed($"still short of {count} {item} after 64 crafts");
    }

    /// <summary>Opens a crafting table nearby, putting one down if there is none.</summary>
    private static async Task<TaskResult> OpenTable(Bot bot)
    {
        if (FindTable(bot) is not { } table)
        {
            if (FindSpotForTable(bot) is not { } spot)
                return TaskResult.Failed("no room next to the bot to put a crafting table down");

            var placed = await bot.Run(new PlaceBlockTask(Item.CraftingTable, spot));

            if (placed.IsFailure)
                return placed;

            table = spot;
        }

        return await bot.Run(new OpenContainerTask(table));
    }

    private static Vector3i? FindTable(Bot bot)
        => bot.World
            .FindBlocks(bot.Player.Position.ToBlockPosition(), TableSearchRadius, state => state.Block == Block.CraftingTable, limit: 1)
            .Cast<Vector3i?>()
            .FirstOrDefault();

    private static Vector3i? FindSpotForTable(Bot bot)
    {
        var feet = bot.Player.Position.ToBlockPosition();

        foreach (var offset in TableSpots)
        {
            var spot = feet + offset;

            if (bot.World.GetBlock(spot)?.Block is Block.Air or Block.CaveAir
                && BlockCollision.IsSolid(bot.World.GetBlock(spot + new Vector3i(0, -1, 0))))
                return spot;
        }

        return null;
    }
}
