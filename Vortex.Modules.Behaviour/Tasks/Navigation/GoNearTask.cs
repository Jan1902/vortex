using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Tasks.Navigation;

/// <summary>
/// Stands next to a block or on it, at most <c>range</c> blocks off sideways
/// and one up or down. For things like items, which do not need an exact spot.
/// </summary>
public class GoNearTask(Vector3i target, int range = 1) : BotTask
{
    public override string Description
        => $"go near {target.X} {target.Y} {target.Z}";

    public override bool IsDone(Bot bot)
    {
        var position = bot.Player.Position;

        // From the player's position rather than its block, so that the far
        // edge of the next block over does not count as next to it.
        return bot.Player.IsPositionSynchronized
            && Math.Abs(position.X - (target.X + 0.5)) <= range + 0.3
            && Math.Abs(position.Z - (target.Z + 0.5)) <= range + 0.3
            && Math.Abs(position.ToBlockPosition().Y - target.Y) <= 1;
    }

    public override Task<TaskResult> RunAsync(Bot bot)
        => Routes.WalkAsync(
            bot,
            (from, previous) => bot.Pathfinder.FindRouteNear(from, target, range, Routes.Capabilities(bot), previous),
            () => IsDone(bot),
            $"get near {target.X} {target.Y} {target.Z}");
}
