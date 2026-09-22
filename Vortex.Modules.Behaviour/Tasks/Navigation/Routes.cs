using Microsoft.Extensions.Logging;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Tasks.Blocks;
using Vortex.Modules.Navigation.Abstraction;
using Vortex.Modules.Player.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Tasks.Navigation;

/// <summary>
/// Walks routes the pathfinder plans, one move at a time. Shared by every task
/// that goes somewhere.
/// </summary>
internal static class Routes
{
    /// <summary>How many moves a walk may take before it is given up as going nowhere.</summary>
    public const int MaxSteps = 300;

    private const double EyeLevel = 1.8;

    /// <summary>What routes may ask of the player.</summary>
    public static MovementCapabilities Capabilities(Bot bot)
        => bot.MayDig ? MovementCapabilities.Digging : MovementCapabilities.Athletic;

    /// <summary>
    /// Walks until <paramref name="arrived"/> holds, searching the route again
    /// after every move so that it follows whatever the world does meanwhile.
    /// </summary>
    /// <param name="plan">Searches the route from where the player is.</param>
    /// <param name="arrived">Whether the walk is over.</param>
    /// <param name="goal">What the walk is to, for messages.</param>
    public static async Task<TaskResult> WalkAsync(Bot bot, Func<Route?> plan, Func<bool> arrived, string goal)
    {
        for (var step = 0; step < MaxSteps; step++)
        {
            if (arrived())
                return TaskResult.Success();

            bot.Cancellation.ThrowIfCancellationRequested();

            if (plan() is not { } route)
                return TaskResult.Failed($"no way to {goal}");

            var moved = await StepAsync(bot, route);

            if (moved.IsFailure)
                return moved;
        }

        return TaskResult.Failed($"still not {goal} after {MaxSteps} moves");
    }

    /// <summary>Takes the next move of a route; with none left, steps into the middle of the block it ends on.</summary>
    private static async Task<TaskResult> StepAsync(Bot bot, Route route)
    {
        var from = route.Origin ?? bot.Player.Position.ToBlockPosition();

        // A run of plain walking in a straight line is one movement, not one per block.
        var move = route.Next;
        var to = move switch
        {
            null => from,
            Walk => route.FurthestWalk(from)!,
            _ => move.To,
        };

        bot.Logger.LogDebug("{Remaining} move(s) left, {Move} to {X} {Y} {Z}", route.Moves.Count, move?.GetType().Name ?? "settle", to.X, to.Y, to.Z);

        // A way that goes through blocks is walked once they are gone. Breaking
        // them is a task of its own; the next round then finds a plain way.
        if (move is MineThrough through)
        {
            foreach (var block in through.Blocking)
            {
                var broken = await bot.Run(new MineBlockTask(block));

                if (broken.IsFailure)
                    return broken;
            }

            return TaskResult.Success();
        }

        var centre = new Vector3d(to.X + 0.5, to.Y, to.Z + 0.5);
        var movement = bot.Movement;

        bot.Player.LookAt(centre + new Vector3d(0, EyeLevel, 0));

        using var registration = bot.Cancellation.Register(movement.Stop);

        var startedIn = bot.Player.Position.ToBlockPosition();

        var result = await (move switch
        {
            null or Walk => movement.WalkTo(centre),
            StepUp => movement.StepUpTo(centre),
            Drop => movement.DropTo(centre),
            JumpGap jump => movement.JumpTo(EdgeOf(from, to), centre, jump.Sprinting ? MovementMode.Sprint : MovementMode.Walk),
            _ => Task.FromResult(MovementResult.Blocked),
        });

        return result switch
        {
            MovementResult.Arrived => TaskResult.Success(),

            // Blocked after getting somewhere, or moved by the server: the next
            // search starts from wherever the player is now.
            MovementResult.Blocked when bot.Player.Position.ToBlockPosition() != startedIn => TaskResult.Success(),
            MovementResult.Desynced => TaskResult.Success(),

            MovementResult.Blocked => TaskResult.Failed($"blocked on the way to {to.X} {to.Y} {to.Z}"),
            _ => TaskResult.Cancelled(),
        };
    }

    /// <summary>The edge of the block being stood on, towards where a jump goes: where it takes off.</summary>
    private static Vector3d EdgeOf(Vector3i from, Vector3i towards)
    {
        var step = towards - from;
        var length = Math.Sqrt((double)(step.X * step.X + step.Z * step.Z));

        return length < 1e-6
            ? new Vector3d(from.X + 0.5, from.Y, from.Z + 0.5)
            : new Vector3d(from.X + 0.5 + step.X / length * 0.5, from.Y, from.Z + 0.5 + step.Z / length * 0.5);
    }
}
