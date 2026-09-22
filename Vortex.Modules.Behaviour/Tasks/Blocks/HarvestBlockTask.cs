using Vortex.Data;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Tasks.Helper;
using Vortex.Modules.Behaviour.Tasks.Items;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Tasks.Blocks;

/// <summary>Breaks a block and picks up what it drops.</summary>
public class HarvestBlockTask(Vector3i target) : BotTask
{
    /// <summary>How far around the block drops are looked for.</summary>
    private const double DropRadius = 4;

    public override string Description
        => $"harvest the block at {target.X} {target.Y} {target.Z}";

    public override async Task<TaskResult> RunAsync(Bot bot)
    {
        // Breaking stone by hand takes ages and gives nothing; say so instead.
        if (bot.World.GetBlock(target) is { } state
            && LootTables.PossibleDrops(state, ToolChoice.Best(state.Block, bot.Inventory).Tool).Count == 0
            && BlockDrops.Tools.Any(tool => LootTables.PossibleDrops(state, tool).Count > 0))
            return TaskResult.Failed($"{state.Block} drops nothing without a better tool");

        var mined = await bot.Run(new MineBlockTask(target));

        if (mined.IsFailure)
            return mined;

        // Best effort: an item that is not picked up is not worth failing over.
        await bot.Run(new CollectItemsTask(new Vector3d(target.X + 0.5, target.Y, target.Z + 0.5), DropRadius));

        return TaskResult.Success();
    }
}
