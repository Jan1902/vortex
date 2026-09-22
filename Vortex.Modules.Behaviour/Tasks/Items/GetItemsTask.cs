using Vortex.Data;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Tasks.Blocks;
using Vortex.Modules.Behaviour.Tasks.Container;
using Vortex.Modules.Behaviour.Tasks.Helper;
using Vortex.Modules.Entities.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Tasks.Items;

/// <summary>
/// Gets a number of items, from wherever they can be had.
/// </summary>
/// <remarks>
/// <para>
/// Tries, in this order: a chest the bot has seen them in, crafting them,
/// mining a block that drops them, picking them up off the ground. The first
/// that works counts; the ways that failed are not tried again in this run.
/// That is the whole of it -- no planner, no costs.
/// </para>
/// <para>
/// Any of the kinds asked for will do, so "four planks" is happy with birch.
/// </para>
/// </remarks>
public class GetItemsTask(IReadOnlySet<Item> wanted, int count) : BotTask
{
    /// <summary>How far around the bot blocks and items are looked for.</summary>
    private const int SearchRadius = 32;

    /// <summary>How many times to try something before giving up on the whole thing.</summary>
    private const int MaxRounds = 32;

    public GetItemsTask(Item item, int count)
        : this(new HashSet<Item> { item }, count)
    {
    }

    /// <summary>The kinds that count towards this.</summary>
    public IReadOnlySet<Item> Wanted => wanted;

    public override string Description
        => $"get {count} {Stacks.Describe(wanted)}";

    public override bool IsDone(Bot bot)
        => Stacks.CountIn(wanted, bot.Inventory) >= count;

    public override async Task<TaskResult> RunAsync(Bot bot)
    {
        // Already fetching exactly these further up: going round again would
        // only find the same ways and end in itself.
        if (bot.RunningTasks.OfType<GetItemsTask>().Count(other => other.Wanted.SetEquals(wanted)) > 1)
            return TaskResult.Failed($"already getting {Stacks.Describe(wanted)}");

        var tried = new HashSet<string>();

        for (var round = 0; round < MaxRounds; round++)
        {
            if (IsDone(bot))
                return TaskResult.Success();

            var missing = count - Stacks.CountIn(wanted, bot.Inventory);

            if (await FromChest(bot, tried)
                || await FromCrafting(bot, tried, missing)
                || await FromMining(bot, tried)
                || await FromGround(bot, tried))
                continue;

            return TaskResult.Failed($"no way to get {missing} more {Stacks.Describe(wanted)}");
        }

        return TaskResult.Failed($"still short of {count} {Stacks.Describe(wanted)} after {MaxRounds} tries");
    }

    /// <summary>Out of a container the bot has seen them in, nearest first.</summary>
    private async Task<bool> FromChest(Bot bot, HashSet<string> tried)
    {
        foreach (var chest in bot.Chests.Holding(wanted, bot.Player.Position))
        {
            if (!tried.Add($"chest {chest.X} {chest.Y} {chest.Z}"))
                continue;

            if ((await bot.Run(new FetchFromChestTask(chest, wanted, count))).IsSuccess)
                return true;
        }

        return false;
    }

    /// <summary>By crafting, the kinds whose ingredients are at hand first.</summary>
    private async Task<bool> FromCrafting(Bot bot, HashSet<string> tried, int missing)
    {
        // Nothing is made out of what is being fetched further up, and nothing
        // is crafted that is already being crafted up there either.
        var busy = BeingGot(bot);
        var crafting = bot.RunningTasks.OfType<CraftTask>().Select(task => task.Item).ToHashSet();

        var candidates = wanted
            .Where(item => !crafting.Contains(item))
            .Select(item => (Item: item, Recipe: CraftingPlanner.Choose(item, bot.Inventory.Count, busy)))
            .Where(candidate => candidate.Recipe is not null)
            .OrderBy(candidate => CraftingPlanner.Needs(candidate.Recipe!)
                .All(need => CraftingPlanner.Available(need.Ingredient, bot.Inventory.Count) >= need.Count) ? 0 : 1)
            .ThenBy(candidate => candidate.Item)
            .Select(candidate => candidate.Item);

        foreach (var item in candidates)
        {
            if (!tried.Add($"craft {item}"))
                continue;

            if ((await bot.Run(new CraftTask(item, bot.Inventory.Count(item) + missing))).IsSuccess)
                return true;
        }

        return false;
    }

    /// <summary>By breaking one of the nearest blocks that drops them, fetching a tool first if one is needed.</summary>
    private async Task<bool> FromMining(Bot bot, HashSet<string> tried)
    {
        var producers = BlockDrops.Producing(wanted);

        if (producers.Count == 0)
            return false;

        var blocks = bot.World.FindBlocks(
            bot.Player.Position.ToBlockPosition(),
            SearchRadius,
            state => producers.Contains(state.Block),
            limit: 8);

        foreach (var block in blocks)
        {
            if (!tried.Add($"mine {block.X} {block.Y} {block.Z}") || bot.World.GetBlock(block) is not { } state)
                continue;

            if (!LootTables.PossibleDrops(state, ToolChoice.Best(state.Block, bot.Inventory).Tool).Overlaps(wanted))
            {
                var tools = BlockDrops.ToolsFor(state, wanted);

                // Nothing gets these out of that block, or no tool to be had:
                // another block of the same kind will not go any better.
                if (tools.Count == 0 || (await bot.Run(new GetItemsTask(tools, 1))).IsFailure)
                    return false;
            }

            if ((await bot.Run(new HarvestBlockTask(block))).IsSuccess)
                return true;
        }

        return false;
    }

    /// <summary>By picking up one lying nearby.</summary>
    private async Task<bool> FromGround(Bot bot, HashSet<string> tried)
    {
        var lying = bot.Entities.Entities
            .Where(entity => entity.Type == EntityType.Item
                && entity.Item is { } stack
                && wanted.Contains(stack.Item)
                && entity.Position.DistanceTo(bot.Player.Position) <= SearchRadius)
            .OrderBy(entity => entity.Position.DistanceTo(bot.Player.Position));

        foreach (var item in lying)
        {
            if (!tried.Add($"pick up {item.Id}"))
                continue;

            var before = Stacks.CountIn(wanted, bot.Inventory);

            await bot.Run(new CollectItemsTask(item.Position, 2));

            if (Stacks.CountIn(wanted, bot.Inventory) > before)
                return true;
        }

        return false;
    }

    /// <summary>What is being got or crafted further up, and so is no way to get this.</summary>
    internal static HashSet<Item> BeingGot(Bot bot)
    {
        var busy = new HashSet<Item>();

        foreach (var task in bot.RunningTasks)
            switch (task)
            {
                case GetItemsTask get:
                    busy.UnionWith(get.Wanted);
                    break;

                case CraftTask craft:
                    busy.Add(craft.Item);
                    break;
            }

        return busy;
    }
}
