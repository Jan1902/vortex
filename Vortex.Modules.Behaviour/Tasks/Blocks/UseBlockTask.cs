using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Tasks.Helper;
using Vortex.Modules.Behaviour.Tasks.Navigation;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Tasks.Blocks;

/// <summary>Right-clicks a block: flips a lever, opens a door, presses a button.</summary>
public class UseBlockTask(Vector3i target) : BotTask
{
    public override string Description
        => $"use the block at {target.X} {target.Y} {target.Z}";

    public override async Task<TaskResult> RunAsync(Bot bot)
    {
        var reached = await bot.Run(new WithinReachTask(target));

        if (reached.IsFailure)
            return reached;

        var face = await Aim.AtBlockAsync(bot.Player, target, bot.Cancellation);

        return await bot.Interaction.UseItemOnBlockAsync(target, face, cancellationToken: bot.Cancellation)
            ? TaskResult.Success()
            : TaskResult.Failed($"the server did not let the bot use {target.X} {target.Y} {target.Z}");
    }
}
