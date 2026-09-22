using Microsoft.Extensions.Logging;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Navigation.Abstraction;
using Vortex.Modules.Player.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Tasks.Navigation;

/// <summary>
/// Stands on a particular block, walking there around whatever is in the way.
/// </summary>
/// <remarks>
/// <para>
/// This is the task to name when the bot has to <em>be</em> somewhere. Use
/// <see cref="WithinReachTask"/> instead when it only has to get close enough to
/// touch something -- that one walks in a straight line and does not care where
/// it ends up, as long as the target is in reach.
/// </para>
/// <para>
/// The route is searched again on every step rather than being planned once and
/// followed. That costs a search per step, and buys the thing that matters here:
/// nothing is remembered, so a route that falls apart underneath the bot -- a
/// block broken, a chunk that only just arrived -- is simply replaced by the
/// next one, with no stale plan to notice and discard.
/// </para>
/// <para>
/// Each move the route names is handed to the movement it is: the search decided
/// to jump that gap or drop off that ledge, and carrying the decision out is not
/// the place to second-guess it. What jumping actually involves -- when to leave
/// the ground, when to stop pushing -- is the movement's own business and none
/// of this task's.
/// </para>
/// </remarks>
public class GoToTask(
    Vector3i target,
    IPlayerManager player,
    IMovementController movement,
    IPathfinder pathfinder,
    MovementCapabilities capabilities,
    ILogger<GoToTask> logger) : BotTask
{
    public override string Description
        => $"stand at {target.X} {target.Y} {target.Z}";

    public override bool IsSatisfied()
        // Not while a server correction is being applied: the position is being
        // rewritten in that window, and arriving is not something to conclude
        // from a number that is about to change.
        => player.IsPositionSynchronized
        && player.Position.ToBlockPosition() == target;

    public override async Task<TaskResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        var route = pathfinder.FindRoute(player.Position, target, capabilities);

        if (route is null)
        {
            var at = player.Position.ToBlockPosition();

            return TaskResult.Failed($"no way from {at.X} {at.Y} {at.Z} to {target.X} {target.Y} {target.Z}");
        }

        // Where the route starts is not always the block the player's middle is
        // over, so take the planner's word for it: aiming a walk from the wrong
        // block cuts corners the route never went round.
        var from = route.Origin ?? player.Position.ToBlockPosition();

        if (route.Next is not { } move)
        {
            // Nothing left to walk, and yet the goal is not reached: the player
            // is standing on the right block with its middle hanging over the
            // next one. Stepping into the middle settles it, where returning
            // success would just have the runner ask again at full speed.
            logger.LogDebug("Already on {Target}, stepping into the middle of it", target);

            return await Carry(move: null, target, target, cancellationToken);
        }

        // A run of blocks in a line is one walk, not one walk per block: the
        // route is only re-searched between movements, so covering ten blocks in
        // ten movements means ten searches to arrive at the same answer. Only
        // plain walking runs together; every other move has to be aimed at
        // afresh, which is exactly why the route names them.
        var step = move is Walk ? route.FurthestWalk(from)! : move.To;

        // The runner's trace can only say that this task acted; which move it
        // picked and how far there is to go is knowledge only this task has.
        // Watching the count is also how a re-route shows up -- it jumps rather
        // than counting down.
        logger.LogDebug("{Remaining} move(s) left to {Target}, {Move} {Run} block(s) to {X} {Y} {Z}",
            route.Moves.Count,
            $"{target.X} {target.Y} {target.Z}",
            move.GetType().Name,
            Math.Abs(step.X - from.X) + Math.Abs(step.Z - from.Z),
            step.X, step.Y, step.Z);

        return await Carry(move, from, step, cancellationToken);
    }

    /// <summary>
    /// Runs the movement a planned move calls for, and reads what came of it.
    /// </summary>
    /// <remarks>
    /// One line per move, because the route already decided. Anything the search
    /// can plan but the player cannot yet do falls through to a refusal here, so
    /// that such a route fails where it was asked for rather than somewhere
    /// further down.
    /// </remarks>
    private async Task<TaskResult> Carry(
        Move? move,
        Vector3i from,
        Vector3i step,
        CancellationToken cancellationToken)
    {
        var centre = Centre(step);

        player.LookAt(centre + new Vector3d(0, EyeLevel, 0));

        using var registration = cancellationToken.Register(movement.Stop);

        var startedIn = player.Position.ToBlockPosition();

        var result = await (move switch
        {
            null or Walk => movement.WalkTo(centre),
            StepUp => movement.StepUpTo(centre),
            Drop => movement.DropTo(centre),

            // The run-up has to carry on past the edge of the block being stood
            // on, so that is where the jump goes from. Where the ground ends is
            // something the route knows and the controller does not.
            JumpGap jump => movement.JumpTo(
                EdgeOf(from, step),
                centre,
                jump.Sprinting ? MovementMode.Sprint : MovementMode.Walk),

            _ => Task.FromResult(MovementResult.Blocked)
        });

        return result switch
        {
            MovementResult.Arrived => TaskResult.Success(),

            // Something got in the way part-way along a run -- a block placed
            // while the bot was walking, most likely. It covered ground before
            // it stopped, so this is not a dead end: the next round searches
            // again from where it actually got to and goes round.
            MovementResult.Blocked when player.Position.ToBlockPosition() != startedIn
                => TaskResult.Success(),

            MovementResult.Blocked => TaskResult.Failed(
                $"blocked on the way to {centre.X:F1} {centre.Y:F1} {centre.Z:F1}"),

            // The server moved the player. Nothing failed, nothing was achieved:
            // the next round searches again from wherever it actually is. The
            // runner's round limit stops this if it never settles.
            MovementResult.Desynced => TaskResult.Success(),

            _ => TaskResult.Cancelled()
        };
    }

    /// <summary>Where to look, so the bot faces the way it walks.</summary>
    private const double EyeLevel = 1.8;

    private static Vector3d Centre(Vector3i block)
        => new(block.X + 0.5, block.Y, block.Z + 0.5);

    /// <summary>
    /// The far edge of the block being stood on, in the direction of travel:
    /// the last solid ground before the gap.
    /// </summary>
    private static Vector3d EdgeOf(Vector3i from, Vector3i towards)
    {
        var step = towards - from;
        var length = Math.Sqrt((double)(step.X * step.X + step.Z * step.Z));

        if (length < 1e-6)
            return Centre(from);

        // Half a block along the direction of travel, so a diagonal leaves from
        // the corner rather than from the middle of a side.
        return new Vector3d(
            from.X + 0.5 + step.X / length * 0.5,
            from.Y,
            from.Z + 0.5 + step.Z / length * 0.5);
    }
}
