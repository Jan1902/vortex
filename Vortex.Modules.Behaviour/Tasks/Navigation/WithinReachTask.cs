using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Tasks.Helper;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Tasks.Navigation;

/// <summary>Gets close enough to a block to break it, use it or place against it.</summary>
public class WithinReachTask(Vector3i target) : BotTask
{
    /// <summary>How close counts as in reach, from the eyes to the block's centre. A little under the server's limit.</summary>
    public const double Reach = 4.0;

    private const double EyeHeight = 1.62;

    public override string Description
        => $"get within reach of {target.X} {target.Y} {target.Z}";

    /// <remarks>
    /// Close enough is not enough on its own: some part of the block has to be
    /// in sight too, or the bot would be acting on it through a wall.
    /// </remarks>
    public override bool IsDone(Bot bot)
    {
        var eyes = bot.Player.Position + new Vector3d(0, EyeHeight, 0);

        return bot.Player.IsPositionSynchronized
            && eyes.DistanceTo(new Vector3d(target.X + 0.5, target.Y + 0.5, target.Z + 0.5)) <= Reach
            && Aim.Sight(bot, target) is not null;
    }

    public override Task<TaskResult> RunAsync(Bot bot)
        => Routes.WalkAsync(
            bot,
            // Planned from block middles, so a bit shorter than the real reach.
            (from, previous) => bot.Pathfinder.FindRouteWithinReach(from, target, Reach - 0.5, Routes.Capabilities(bot), previous),
            () => IsDone(bot),
            $"get within reach of {target.X} {target.Y} {target.Z}");
}
