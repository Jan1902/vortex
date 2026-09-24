using Microsoft.Extensions.Logging;
using Vortex.Data;
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
/// <remarks>
/// <para>
/// A route is searched once and then followed, not searched again after every
/// move. What keeps it honest is that the next few moves are checked against
/// the world before each one is made, by the same code that planned them. As
/// long as they still hold, the route stands; the moment one does not --
/// something built in the way, a floor dug out -- it is searched again from
/// where the player is, keeping to the old route wherever that still works.
/// </para>
/// <para>
/// A route the pathfinder had to cut short for time is walked all the same, and
/// the rest of the way is searched from where it ends while the player is still
/// on its last stretch, so that it does not have to stop and wait at the end.
/// </para>
/// </remarks>
internal static class Routes
{
    /// <summary>How many times a walk may search for a route before it is given up as going nowhere.</summary>
    public const int MaxSearches = 300;

    /// <summary>
    /// How many moves beyond the one about to be made are checked against the
    /// world first.
    /// </summary>
    /// <remarks>
    /// More than one, so that a change just ahead is noticed while there is
    /// still room to go another way, rather than on arriving in front of it.
    /// Not the whole route: far ahead, the world has time to change again
    /// before the player gets there, and it will be checked then.
    /// </remarks>
    private const int Lookahead = 5;

    /// <summary>
    /// How few moves have to be left of a route that was cut short for the rest
    /// of the way to be searched for in the background.
    /// </summary>
    /// <remarks>
    /// Late enough that the world near the end has been seen by then, early
    /// enough that a search of a second or two is done before the player
    /// arrives.
    /// </remarks>
    private const int PlanAheadWithin = 12;

    private const double EyeLevel = 1.8;

    /// <summary>
    /// What routes may ask of the player, and what it carries to do it with.
    /// </summary>
    /// <remarks>
    /// Asked again for every search, because the inventory changes on the way:
    /// blocks get used up, and a pickaxe picked up makes stone worth digging.
    /// </remarks>
    public static MovementCapabilities Capabilities(Bot bot)
        => bot.MayDig
            ? MovementCapabilities.Building with { Loadout = new Loadout(Tools(bot), Building.Count(bot)) }
            : MovementCapabilities.Athletic;

    /// <summary>Everything the bot carries that breaks some block faster than a bare hand.</summary>
    private static IReadOnlyList<Item> Tools(Bot bot)
        => bot.Inventory.Find(stack => stack.Item.Tool() is not null)
            .Select(found => found.Stack.Item)
            .Distinct()
            .ToList();

    /// <summary>
    /// Walks until <paramref name="arrived"/> holds, following whatever the
    /// world does meanwhile.
    /// </summary>
    /// <param name="plan">
    /// Searches a route from a position, keeping close to the route it is
    /// handed where there is one.
    /// </param>
    /// <param name="arrived">Whether the walk is over.</param>
    /// <param name="goal">What the walk is to, for messages.</param>
    public static async Task<TaskResult> WalkAsync(Bot bot, Func<Vector3d, Route?, Route?> plan, Func<bool> arrived, string goal)
    {
        var searches = 0;

        // The route being walked, as what is left of it. Null when there is
        // none, and a new one has to be searched.
        Route? route = null;

        // The route given up on, which the next search should keep close to.
        Route? replaced = null;

        // The rest of the way beyond a route that was cut short, being
        // searched while that route is walked.
        Task<Route?>? ahead = null;

        while (true)
        {
            if (arrived())
                return TaskResult.Success();

            bot.Cancellation.ThrowIfCancellationRequested();

            if (route is null)
            {
                if (++searches > MaxSearches)
                    return TaskResult.Failed($"still not {goal} after {MaxSearches} searches");

                route = plan(bot.Player.Position, replaced);
                replaced = null;
                ahead = null;

                if (route is null)
                    return TaskResult.Failed($"no way to {goal}");
            }

            // Nothing left to walk: the last of it was searched for or not, so
            // take the rest of the way if it is ready, and search again if not.
            if (route.Moves.Count == 0)
            {
                var continued = route.Truncated ? await Continuation(bot, ahead, route) : null;

                ahead = null;

                if (continued is null)
                {
                    // Standing where the route ends: settle in the middle of the
                    // block before anything is searched from it.
                    var settled = await StepAsync(bot, route, 0);

                    if (settled.Result == StepResult.Failed)
                        return settled.Outcome;

                    route = null;

                    continue;
                }

                route = continued;
            }

            var from = route.Origin ?? bot.Player.Position.ToBlockPosition();

            // How many moves the next movement covers: a run of plain walking in
            // a straight line is one movement, not one per block.
            var covers = Math.Max(1, route.StraightWalkLength(from));

            if (FirstChanged(bot, route, covers + Lookahead, Capabilities(bot)) is { } changed)
            {
                bot.Logger.LogDebug("The way changed at {To}; searching again", changed.To);

                replaced = route;
                route = null;

                continue;
            }

            if (route.Truncated && ahead is null && route.Moves.Count <= PlanAheadWithin && route.Destination is { } end)
            {
                bot.Logger.LogDebug("Searching on from {X} {Y} {Z} while walking there", end.X, end.Y, end.Z);

                ahead = Task.Run(() => plan(Centre(end), null));
            }

            var moved = await StepAsync(bot, route, covers);

            switch (moved.Result)
            {
                case StepResult.Moved:
                    route = route.Skip(covers);
                    break;

                // Somewhere other than planned, or through blocks that are gone
                // now: either way the rest is searched from where the player
                // is, staying close to what was planned.
                case StepResult.Elsewhere:
                case StepResult.Dug:
                    replaced = route;
                    route = null;
                    break;

                default:
                    return moved.Outcome;
            }
        }
    }

    /// <summary>
    /// The first of the coming moves that can no longer be made, or null if
    /// they all still can.
    /// </summary>
    /// <remarks>
    /// Stops at the first move through blocks: after digging, the world around
    /// it is not what it is now, and what comes after it is searched afresh
    /// anyway.
    /// </remarks>
    private static Move? FirstChanged(Bot bot, Route route, int count, MovementCapabilities capabilities)
    {
        if (route.Origin is not { } from)
            return null;

        for (var i = 0; i < Math.Min(count, route.Moves.Count); i++)
        {
            var move = route.Moves[i];

            if (!bot.Pathfinder.CanStillMake(from, move, capabilities))
                return move;

            if (move is MineThrough)
                break;

            from = move.To;
        }

        return null;
    }

    /// <summary>
    /// The rest of the way beyond a route that was cut short, if it was searched
    /// for and picks up where the player now is.
    /// </summary>
    private static async Task<Route?> Continuation(Bot bot, Task<Route?>? ahead, Route finished)
    {
        if (ahead is null)
            return null;

        Route? continued;

        try
        {
            continued = await ahead.WaitAsync(bot.Cancellation);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            bot.Logger.LogDebug(exception, "Searching on ahead failed; searching again from here");

            return null;
        }

        return continued is { Moves.Count: > 0 } && continued.Origin == finished.Origin
            ? continued
            : null;
    }

    /// <summary>
    /// Takes the next movement of a route, covering the given number of moves;
    /// with none, steps into the middle of the block it ends on.
    /// </summary>
    private static async Task<(StepResult Result, TaskResult Outcome)> StepAsync(Bot bot, Route route, int covers)
    {
        var from = route.Origin ?? bot.Player.Position.ToBlockPosition();

        var move = route.Next;
        var to = move switch
        {
            null => from,
            _ => route.Moves[covers - 1].To,
        };

        bot.Logger.LogDebug("{Remaining} move(s) left, {Move} to {X} {Y} {Z}", route.Moves.Count, move?.GetType().Name ?? "settle", to.X, to.Y, to.Z);

        // A way that goes through blocks is walked once they are gone. Breaking
        // them is a task of its own; the next round then finds a plain way.
        if (move is MineThrough through)
        {
            // Digging out the floor: from the middle of the block, so the
            // player drops onto what the route expects below it. Standing on
            // the edge -- where a jump tends to land -- it would drop past the
            // side instead, as far down as that happens to go.
            if (through.Blocking.Contains(from + new Vector3i(0, -1, 0))
                && await bot.Movement.WalkTo(Centre(from)) == MovementResult.Cancelled)
            {
                return (StepResult.Failed, TaskResult.Cancelled());
            }

            foreach (var block in through.Blocking)
            {
                var broken = await bot.Run(new MineBlockTask(block));

                if (broken.IsFailure)
                    return (StepResult.Failed, broken);
            }

            return (StepResult.Dug, TaskResult.Success());
        }

        // Moves that put a block down are more than a movement: they need the
        // block in hand and placing at the right moment.
        if (move is Pillar or Bridge)
        {
            var built = move is Pillar
                ? await Building.PillarAsync(bot, from)
                : await Building.BridgeAsync(bot, from, to);

            bot.Cancellation.ThrowIfCancellationRequested();

            return built
                ? (StepResult.Moved, TaskResult.Success())
                : (StepResult.Elsewhere, TaskResult.Success());
        }

        var centre = Centre(to);
        var movement = bot.Movement;

        bot.Player.LookAt(centre + new Vector3d(0, EyeLevel, 0));

        using var registration = bot.Cancellation.Register(movement.Stop);

        var startedIn = bot.Player.Position.ToBlockPosition();

        var result = await (move switch
        {
            null or Walk => movement.WalkTo(centre),
            StepUp => movement.StepUpTo(centre),
            Drop => movement.DropTo(centre),
            Swim => movement.SwimTo(centre),
            JumpGap jump => movement.JumpTo(EdgeOf(from, to), centre, jump.Sprinting ? MovementMode.Sprint : MovementMode.Walk),
            _ => Task.FromResult(MovementResult.Blocked),
        });

        return result switch
        {
            MovementResult.Arrived => (StepResult.Moved, TaskResult.Success()),

            // Blocked after getting somewhere, or moved by the server: the next
            // search starts from wherever the player is now.
            MovementResult.Blocked when bot.Player.Position.ToBlockPosition() != startedIn => (StepResult.Elsewhere, TaskResult.Success()),
            MovementResult.Desynced => (StepResult.Elsewhere, TaskResult.Success()),

            MovementResult.Blocked => (StepResult.Failed, TaskResult.Failed($"blocked on the way to {to.X} {to.Y} {to.Z}")),
            _ => (StepResult.Failed, TaskResult.Cancelled()),
        };
    }

    private static Vector3d Centre(Vector3i block)
        => new(block.X + 0.5, block.Y, block.Z + 0.5);

    /// <summary>The edge of the block being stood on, towards where a jump goes: where it takes off.</summary>
    private static Vector3d EdgeOf(Vector3i from, Vector3i towards)
    {
        var step = towards - from;
        var length = Math.Sqrt((double)(step.X * step.X + step.Z * step.Z));

        return length < 1e-6
            ? new Vector3d(from.X + 0.5, from.Y, from.Z + 0.5)
            : new Vector3d(from.X + 0.5 + step.X / length * 0.5, from.Y, from.Z + 0.5 + step.Z / length * 0.5);
    }

    /// <summary>How a movement went, as far as following the route is concerned.</summary>
    private enum StepResult
    {
        /// <summary>Made as planned; the route carries on from its end.</summary>
        Moved,

        /// <summary>Ended up somewhere the route did not say.</summary>
        Elsewhere,

        /// <summary>Broke the blocks in the way, without walking through yet.</summary>
        Dug,

        /// <summary>Did not work; the walk is over.</summary>
        Failed,
    }
}
