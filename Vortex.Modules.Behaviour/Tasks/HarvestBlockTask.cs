using Vortex.Data;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Inventory.Abstraction;
using Vortex.Modules.World.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Tasks;

/// <summary>
/// Breaks a block and picks up what it drops.
/// </summary>
/// <remarks>
/// Before breaking anything it asks the block's loot table whether breaking it
/// with the best tool at hand drops anything at all -- stone mined by hand does
/// not -- and gives up with the reason instead of digging for nothing.
/// </remarks>
public class HarvestBlockTask(
    Vector3i target,
    IWorldManager world,
    IInventoryManager inventory,
    Func<Vector3i, MineBlockTask> mine,
    Func<Vector3d, double, CollectItemsTask> collect) : BotTask
{
    /// <summary>How far from the block its drops are looked for; they scatter a little.</summary>
    private const double DropRadius = 4;

    private readonly Vector3d _center = new(target.X + 0.5, target.Y + 0.5, target.Z + 0.5);

    public override string Description
        => $"harvest the block at {target.X} {target.Y} {target.Z}";

    public override bool IsSatisfied()
        => mine(target).IsSatisfied() && collect(_center, DropRadius).IsSatisfied();

    public override IEnumerable<BotTask> Dependencies()
    {
        // With nothing to gain, name nothing: the action below then explains why.
        if (DropsNothing())
            yield break;

        yield return mine(target);
        yield return collect(_center, DropRadius);
    }

    public override Task<TaskResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        var block = world.GetBlock(target)?.Block;
        var tool = block is null ? null : ToolChoice.Best(block.Value, inventory).Tool;

        return Task.FromResult(TaskResult.Failed(
            $"breaking {block} with {tool?.ToString() ?? "the bare hand"} drops nothing; it needs a better tool"));
    }

    /// <summary>
    /// Whether the block is still there and breaking it with the best tool at
    /// hand would yield nothing, going by its loot table.
    /// </summary>
    private bool DropsNothing()
    {
        if (world.GetBlock(target) is not { } state || mine(target).IsSatisfied())
            return false;

        var tool = ToolChoice.Best(state.Block, inventory).Tool;

        return LootTables.PossibleDrops(state, tool).Count == 0;
    }
}
