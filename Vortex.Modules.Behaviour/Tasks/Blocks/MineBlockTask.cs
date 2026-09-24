using Microsoft.Extensions.Logging;
using Vortex.Data;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Tasks.Helper;
using Vortex.Modules.Behaviour.Tasks.Navigation;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Tasks.Blocks;

/// <summary>Breaks a block with the best tool at hand. Does not pick up what it drops; see <see cref="HarvestBlockTask"/>.</summary>
public class MineBlockTask(Vector3i target) : BotTask
{
    public override string Description
        => $"break the block at {target.X} {target.Y} {target.Z}";

    public override bool IsDone(Bot bot)
        => bot.World.GetBlock(target) is { } state && IsNothingToBreak(state.Block);

    public override async Task<TaskResult> RunAsync(Bot bot)
    {
        var reached = await bot.Run(new WithinReachTask(target));

        if (reached.IsFailure)
            return reached;

        if (bot.World.GetBlock(target) is not { } state)
            return TaskResult.Failed($"{target.X} {target.Y} {target.Z} is not loaded");

        var (slot, tool) = ToolChoice.Best(state.Block, bot.Inventory);

        await Hold.InMainHandAsync(bot.Inventory, slot);

        if (Mining.BreakTicks(state.Block, tool, onGround: bot.Player.IsOnGround) is not { } ticks)
            return TaskResult.Failed($"{state.Block} cannot be broken");

        if (await Aim.AtBlockAsync(bot, target) is not { } face)
            return TaskResult.Failed($"cannot see the block at {target.X} {target.Y} {target.Z} to break it");

        bot.Logger.LogDebug("Breaking {Block} with {Tool}, {Ticks} ticks", state.Block, tool?.ToString() ?? "the bare hand", ticks);

        if (!await bot.Interaction.DigAsync(target, face, ticks, bot.Cancellation))
            return TaskResult.Failed($"the server did not let the bot break {state.Block}");

        // The server confirms the dig before the block change arrives.
        for (var waited = 0; waited < 20 && !IsDone(bot); waited++)
            await Task.Delay(50, bot.Cancellation);

        return IsDone(bot)
            ? TaskResult.Success()
            : TaskResult.Failed($"the server kept {state.Block} at {target.X} {target.Y} {target.Z}");
    }

    private static bool IsNothingToBreak(Block block)
        => block is Block.Air or Block.CaveAir or Block.VoidAir or Block.Water or Block.Lava;
}
