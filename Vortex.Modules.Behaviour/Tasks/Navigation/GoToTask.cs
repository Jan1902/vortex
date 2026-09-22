using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Tasks.Navigation;

/// <summary>Stands on a particular block.</summary>
public class GoToTask(Vector3i target) : BotTask
{
    public override string Description
        => $"go to {target.X} {target.Y} {target.Z}";

    public override bool IsDone(Bot bot)
        => bot.Player.IsPositionSynchronized && bot.Player.Position.ToBlockPosition() == target;

    public override Task<TaskResult> RunAsync(Bot bot)
        => Routes.WalkAsync(
            bot,
            () => bot.Pathfinder.FindRoute(bot.Player.Position, target, Routes.Capabilities(bot)),
            () => IsDone(bot),
            $"get to {target.X} {target.Y} {target.Z}");
}
