using Microsoft.Extensions.Logging;
using Vortex.Data;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Interaction.Abstraction;
using Vortex.Modules.Inventory.Abstraction;
using Vortex.Modules.Player.Abstraction;
using Vortex.Modules.World.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Tasks;

/// <summary>
/// Breaks a block, with the best tool the hotbar has for it.
/// </summary>
/// <remarks>
/// Done once the block is gone, whoever broke it. What it drops is left lying;
/// <see cref="HarvestBlockTask"/> picks it up as well.
/// </remarks>
public class MineBlockTask(
    Vector3i target,
    IWorldManager world,
    IPlayerManager player,
    IInventoryManager inventory,
    IInteractionManager interaction,
    Func<Vector3i, WithinReachTask> withinReach,
    ILogger<MineBlockTask> logger) : BotTask
{
    /// <summary>Where the player's eyes sit above its feet.</summary>
    private const double EyeHeight = 1.62;

    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(50);

    /// <summary>How long the block has to change after the server confirmed breaking it.</summary>
    private static readonly TimeSpan BlockUpdateWait = TimeSpan.FromSeconds(1);

    public override string Description
        => $"break the block at {target.X} {target.Y} {target.Z}";

    public override bool IsSatisfied()
        => world.GetBlock(target) is { } state && IsNothingToBreak(state.Block);

    public override IEnumerable<BotTask> Dependencies()
    {
        yield return withinReach(target);
    }

    public override async Task<TaskResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        if (world.GetBlock(target) is not { } state)
            return TaskResult.Failed($"{target.X} {target.Y} {target.Z} is not loaded");

        var (slot, tool) = ToolChoice.Best(state.Block, inventory);

        if (slot != inventory.SelectedHotbarSlot)
            await inventory.SelectHotbarSlotAsync(slot);

        if (Mining.BreakTicks(state.Block, tool, onGround: player.IsOnGround) is not { } ticks)
            return TaskResult.Failed($"{state.Block} cannot be broken");

        var eyes = player.Position + new Vector3d(0, EyeHeight, 0);
        var face = BlockFaces.Facing(target, eyes);

        player.LookAt(BlockFaces.Center(target, face));
        await Task.Delay(Tick, cancellationToken);

        logger.LogDebug("Breaking {Block} at {Target} with {Tool}, {Ticks} ticks", state.Block, target, tool?.ToString() ?? "the bare hand", ticks);

        if (!await interaction.DigAsync(target, face, ticks, cancellationToken))
            return TaskResult.Failed($"the server did not confirm breaking {state.Block} at {target.X} {target.Y} {target.Z}");

        var deadline = DateTime.UtcNow + BlockUpdateWait;

        while (!IsSatisfied())
        {
            if (DateTime.UtcNow > deadline)
                return TaskResult.Failed($"the server kept {state.Block} at {target.X} {target.Y} {target.Z}");

            await Task.Delay(Tick, cancellationToken);
        }

        return TaskResult.Success();
    }

    /// <summary>Air, and fluids, which cannot be broken but do not stand in the way of it either.</summary>
    private static bool IsNothingToBreak(Block block)
        => block is Block.Air or Block.CaveAir or Block.VoidAir or Block.Water or Block.Lava;
}
