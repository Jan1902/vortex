using Microsoft.Extensions.Logging;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Player.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Tasks;

/// <summary>
/// Gets close enough to a block to do something with it.
/// </summary>
/// <remarks>
/// <para>
/// This is the task other tasks name when they need to be somewhere. They do not
/// check whether they are in range first -- they name this, and it answers for
/// itself whether that means any walking.
/// </para>
/// <para>
/// It walks in a straight line, in short hops. It cannot get around anything: an
/// obstacle it cannot step over ends it, and routing around one belongs in a
/// task above this one once there is a pathfinder to ask.
/// </para>
/// </remarks>
public class WithinReachTask(
    Vector3i target,
    IPlayerManager player,
    IMovementController movement,
    ILogger<WithinReachTask> logger) : BotTask
{
    /// <summary>
    /// How close counts as in reach. A little under the server's limit, so that
    /// arriving exactly on the boundary does not immediately fall back out of it.
    /// </summary>
    private const double Reach = 4.0;

    /// <summary>
    /// How far to walk in one go. Nothing is re-evaluated while a step runs, so
    /// these stay short enough that the world is looked at again on the way.
    /// </summary>
    private const double StepLength = 1.5;

    /// <summary>Where the player's eyes sit above its feet.</summary>
    private const double EyeHeight = 1.62;

    public override string Description
        => $"be in reach of {target.X} {target.Y} {target.Z}";

    public override bool IsSatisfied()
        => player.IsPositionSynchronized
        && DistanceToTarget() <= Reach;

    public override async Task<TaskResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        var center = Center(target);
        var position = player.Position;

        var direction = new Vector3d(center.X - position.X, 0, center.Z - position.Z);
        var remaining = position.HorizontalDistanceTo(center);

        if (remaining < 1e-3)
            return TaskResult.Failed($"standing directly under or over {target.X} {target.Y} {target.Z}; walking cannot get any closer");

        logger.LogDebug("{Distance:F1} blocks from {Target}, closing {Step:F1}",
            DistanceToTarget(),
            $"{target.X} {target.Y} {target.Z}",
            Math.Min(StepLength, remaining));

        player.LookAt(center);

        using var registration = cancellationToken.Register(movement.Stop);

        // One capped step at a time, so the straight line is re-aimed from
        // wherever the player actually got to.
        var step = Math.Min(StepLength, remaining) / remaining;

        var result = await movement.WalkTo(
            position + direction * step,
            MovementMode.Walk,
            autoJump: true);

        return result switch
        {
            MovementResult.Arrived => TaskResult.Success(),
            MovementResult.Blocked => TaskResult.Failed($"the way to {target.X} {target.Y} {target.Z} is blocked"),

            // Moved by the server, so this step aimed at a stale target. Try
            // again from where the player really is.
            MovementResult.Desynced => TaskResult.Success(),

            _ => TaskResult.Cancelled()
        };
    }

    private double DistanceToTarget()
    {
        var eyes = player.Position + new Vector3d(0, EyeHeight, 0);
        var center = Center(target);

        var dx = eyes.X - center.X;
        var dy = eyes.Y - center.Y;
        var dz = eyes.Z - center.Z;

        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private static Vector3d Center(Vector3i block)
        => new(block.X + 0.5, block.Y + 0.5, block.Z + 0.5);
}
