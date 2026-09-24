using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Vortex.Modules.Navigation.Abstraction;
using Vortex.Modules.Navigation.Movements;
using Vortex.Modules.World.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Navigation;

/// <summary>
/// An A* search over block positions, with a time budget.
/// </summary>
/// <remarks>
/// <para>
/// A node is a block position the player's feet can occupy: two free blocks with
/// something solid underneath. Where the player can go from there is up to the
/// movements it is handed -- walking, stepping up, dropping, jumping, digging --
/// each of which works out its own moves and what they cost. The search only
/// asks all of them and keeps the cheapest way to each position.
/// </para>
/// <para>
/// What it will actually plan is decided by the capabilities it is handed, not
/// by what it can imagine. There are still no doors, no swimming, no ladders, no
/// placing blocks to get across, and no notion of danger beyond what hurts to
/// stand in. Each of those is a movement of its own still to be written.
/// </para>
/// <para>
/// A search that runs out of time does not come back empty-handed if it can
/// help it. It hands back the way to the most promising position it reached,
/// the way Baritone does, so that the bot can start walking while the rest is
/// worked out.
/// </para>
/// </remarks>
internal class AStarPathfinder(IWorldManager world, ILogger<AStarPathfinder> logger, PathfinderOptions? options = null) : IPathfinder
{
    /// <summary>How high above its feet the player's eyes are, which is where reach is measured from.</summary>
    private const double EyeHeight = 1.62;

    /// <summary>
    /// How far above and below the player to look when picking somewhere to
    /// head for on the way to a goal that is not loaded yet.
    /// </summary>
    private const int HeightSearchRange = 4;

    /// <summary>
    /// How far, in blocks, a route cut short has to get the player before it
    /// is worth walking at all.
    /// </summary>
    /// <remarks>
    /// Less than this and the next search starts from nearly the same place and
    /// most likely stops at nearly the same place, which is standing still with
    /// extra steps.
    /// </remarks>
    private const double MinimumProgress = 5;

    /// <summary>
    /// How many positions are looked at between checks of the clock. Reading
    /// it is not free, and neither is a search that overruns by a few
    /// hundred positions.
    /// </summary>
    private const int ClockInterval = 256;

    /// <summary>
    /// How much the way already walked counts against the estimate of what is
    /// left, when picking where a route that is cut short should end.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Weighing the two alike picks the position that looks best by the
    /// search's own measure, which early on is one barely away from the start.
    /// Counting what is behind for less leans towards whatever has got furthest
    /// towards the goal, and the more it is discounted, the further out -- and
    /// the less sure -- the pick gets.
    /// </para>
    /// <para>
    /// So there are several, tried from the most careful up, and the first
    /// whose pick is far enough away to be worth walking is the one taken. These
    /// are the numbers Baritone uses.
    /// </para>
    /// </remarks>
    private static readonly double[] _partialWeights = [1.5, 2, 2.5, 3, 4, 5, 10];

    /// <summary>
    /// Everything the player can do to get from one block to the next. Adding a
    /// new kind of move is adding it here.
    /// </summary>
    private static readonly IMovement[] _movements =
    [
        new Walking(),
        new SteppingUp(),
        new Dropping(),
        new JumpingAcross(),
        new JumpingOffAxis(),
        new Tunnelling(),
        new DiggingUp(),
        new DiggingDown(),
        new Swimming(),
        new Pillaring(),
        new Bridging(),
    ];

    private readonly PathfinderOptions _options = options ?? PathfinderOptions.Default;

    public Route? FindRoute(Vector3d start, Vector3i goal, MovementCapabilities? capabilities = null, Route? previous = null)
    {
        var context = NewContext(capabilities);

        return Search(context, StandingBlockFor(context, start), goal, previous);
    }

    public Route? FindRoute(Vector3i start, Vector3i goal, MovementCapabilities? capabilities = null, Route? previous = null)
    {
        var context = NewContext(capabilities);

        return Search(context, context.CanStandAt(start) ? start : StandingPositionFor(context, start), goal, previous);
    }

    public Route? FindRouteWithinReach(Vector3d start, Vector3i target, double reach, MovementCapabilities? capabilities = null, Route? previous = null)
    {
        var context = NewContext(capabilities);
        var origin = StandingBlockFor(context, start);
        Cell block = target;

        // Not loaded yet: head that way as for any other unknown goal, and look
        // for somewhere to stand once it is known.
        if (world.GetBlock(target) is null)
            return Search(context, origin, target, previous);

        return Explore(
            context,
            origin,
            position => IsWithinReach(position, block, reach)
                && position != block
                && position.Above != block
                && context.CanSee(position, block),
            new Target(block, reach, (int)Math.Ceiling(reach + EyeHeight), Math.Max(0, (int)Math.Floor(reach + 0.5 - EyeHeight))),
            reachesGoal: true,
            label: target,
            previous);
    }

    public Route? FindRouteNear(Vector3d start, Vector3i target, int range, MovementCapabilities? capabilities = null, Route? previous = null)
    {
        var context = NewContext(capabilities);
        var origin = StandingBlockFor(context, start);
        Cell block = target;

        if (world.GetBlock(target) is null)
            return Search(context, origin, target, previous);

        return Explore(
            context,
            origin,
            position => IsNear(position, block, range),
            // The corner of the square is the furthest off the target an
            // arrival can be, and the estimate must not count that as still to go.
            new Target(block, range * Math.Sqrt(2), 1, 1),
            reachesGoal: true,
            label: target,
            previous);
    }

    public bool CanStillMake(Vector3i from, Move move, MovementCapabilities? capabilities = null)
    {
        var context = NewContext(capabilities);
        var steps = new List<Step>();

        foreach (var movement in _movements)
            movement.Expand(context, from, steps);

        foreach (var step in steps)
            if (step.Matches(move))
                return true;

        return false;
    }

    /// <summary>Whether a position is at most some blocks off a target sideways, and at most one up or down.</summary>
    public static bool IsNear(Vector3i position, Vector3i target, int range)
        => IsNear((Cell)position, target, range);

    private static bool IsNear(Cell position, Cell target, int range)
        => Math.Abs(position.X - target.X) <= range
        && Math.Abs(position.Z - target.Z) <= range
        && Math.Abs(position.Y - target.Y) <= 1;

    /// <summary>
    /// Whether a block is within reach of a player standing at a position,
    /// measured from the eyes of one standing in the middle of it.
    /// </summary>
    private static bool IsWithinReach(Cell standing, Cell target, double reach)
    {
        var dx = standing.X - target.X;
        var dy = standing.Y + EyeHeight - (target.Y + 0.5);
        var dz = standing.Z - target.Z;

        return dx * dx + dy * dy + dz * dz <= reach * reach;
    }

    /// <summary>
    /// A fresh view of the world for one search. Fresh every time, so that it
    /// sees whatever has changed since the last one.
    /// </summary>
    private SearchContext NewContext(MovementCapabilities? capabilities)
        => new(new BlockCache(world), capabilities ?? MovementCapabilities.Walking);

    private Route? Search(SearchContext context, Cell origin, Vector3i goal, Route? previous)
    {
        if (origin == goal)
            return new Route([], ReachesGoal: true, Origin: origin.ToVector3i());

        // A goal the client has not been sent yet is not unreachable, it is
        // unknown: chunks arrive as the player approaches, so refusing to move
        // would mean never being able to walk further than the view distance.
        // Head for the furthest known ground along the way instead and ask
        // again from there.
        var reachesGoal = true;
        Cell destination = goal;

        if (world.GetBlock(goal) is null)
        {
            if (NearestKnownTowards(context, origin, goal) is not { } staging)
            {
                logger.LogDebug("No path towards {Goal}: nothing known in that direction", goal);

                return null;
            }

            logger.LogDebug("{Goal} is not loaded yet; heading for {Staging} first", goal, staging);

            destination = staging;
            reachesGoal = false;
        }

        if (!context.CanBeAt(destination))
        {
            logger.LogDebug("No path to {Goal}: nothing to stand on there", destination);

            return null;
        }

        return Explore(context, origin, position => position == destination, new Target(destination, 0, 0, 0), reachesGoal, goal, previous);
    }

    /// <summary>
    /// The A* search itself, towards whatever counts as arriving.
    /// </summary>
    /// <param name="isGoal">Whether standing at a position is arriving.</param>
    /// <param name="target">Where arriving happens, for the estimate of what is left.</param>
    /// <param name="label">What the search is for, in the log.</param>
    /// <param name="previous">The route being replaced, whose positions come cheaper.</param>
    private Route? Explore(
        SearchContext context,
        Cell origin,
        Func<Cell, bool> isGoal,
        Target target,
        bool reachesGoal,
        Vector3i label,
        Route? previous)
    {
        if (isGoal(origin))
            return new Route([], reachesGoal, Origin: origin.ToVector3i());

        var clock = Stopwatch.StartNew();
        var allowed = context.Allowed;

        // What a block of the way left is reckoned to cost: a walk, even where
        // the search may dig. Guessing higher points the search straight at the
        // goal, but then it tunnels through walls it could have walked round
        // for less, because the way round never looked worth trying. Priced as
        // a walk, it finds the cheaper of the two; where that means looking
        // through a lot of rock, the time budget and a route cut short are
        // what keep it from looking for ever.
        // What a block of height still to lose is reckoned to cost. Where the
        // search may dig, the way down may well be through rock, and pricing
        // that as a fall has the search look at every place on the surface
        // above before it tries going down at all.
        var perBlockDown = allowed.Dig ? Costs.DigDownEstimate : Costs.Descend;

        double Estimate(Cell from)
            => Heuristic(from, target, allowed.Diagonals, perBlockDown);

        var favoured = Favoured(previous);

        // A node is a position and nothing else. What a turn costs depends on
        // which way the player was going, but only ever settles a tie, and
        // making the direction part of the node to price it exactly would
        // multiply every position by every way into it.
        var nodes = new List<Node>();
        var index = new Dictionary<Cell, int>();
        var open = new PriorityQueue<int, Priority>(Priority.Comparer);

        nodes.Add(new Node(origin, Cost: 0, Estimate(origin), Parent: -1, Headings.None, Arrival: default, Placed: 0));
        index[origin] = 0;
        open.Enqueue(0, new Priority(nodes[0].Estimate, nodes[0].Estimate));

        var partial = new PartialRoutes(origin);
        var steps = new List<Step>(64);
        var expanded = 0;

        while (open.TryDequeue(out var current, out _))
        {
            ref var node = ref CollectionsMarshal.AsSpan(nodes)[current];

            // The queue has no decrease-key, so a node can sit in it more than
            // once. The first time it comes out carries its best cost, and any
            // later copy is stale.
            if (node.Closed)
                continue;

            if (isGoal(node.Position))
                return Reconstruct(nodes, current, reachesGoal, origin, truncated: false);

            node.Closed = true;
            partial.Consider(current, node);

            var position = node.Position;
            var costSoFar = node.Cost;
            var arrivedHeading = node.Heading;
            var placedSoFar = node.Placed;

            expanded++;

            if ((expanded % ClockInterval == 0 || expanded >= _options.MaxExpandedPositions)
                && IsOutOfTime(clock.Elapsed, expanded, partial, nodes, out var best))
            {
                if (best is not { } furthest)
                {
                    logger.LogDebug("Gave up looking for a path to {Goal} after {Expanded} positions", label, expanded);

                    return null;
                }

                logger.LogDebug(
                    "Out of time looking for a path to {Goal} after {Expanded} positions; taking the way to {Partial} for now",
                    label,
                    expanded,
                    nodes[furthest].Position);

                return Reconstruct(nodes, furthest, reachesGoal, origin, truncated: true);
            }

            steps.Clear();

            foreach (var movement in _movements)
                movement.Expand(context, position, steps);

            foreach (var step in steps)
            {
                // Blocks are counted along the way that got here, not per
                // position: two ways to the same place can have used up
                // different numbers of them, and only the cheaper is kept.
                var placed = placedSoFar + step.Places;

                if (placed > allowed.Loadout.Blocks)
                    continue;

                var stepCost = favoured is not null && favoured.Contains(step.To)
                    ? step.Cost * Costs.FavourPrevious
                    : step.Cost;

                var turning = arrivedHeading != Headings.None && arrivedHeading != step.Heading;
                var cost = costSoFar + stepCost + (turning ? Costs.Turn : 0);

                if (index.TryGetValue(step.To, out var existing))
                {
                    ref var known = ref CollectionsMarshal.AsSpan(nodes)[existing];

                    if (known.Closed || cost >= known.Cost)
                        continue;

                    known.Cost = cost;
                    known.Parent = current;
                    known.Heading = step.Heading;
                    known.Arrival = step;
                    known.Placed = placed;

                    open.Enqueue(existing, new Priority(cost + known.Estimate, known.Estimate));

                    continue;
                }

                var estimate = Estimate(step.To);

                index[step.To] = nodes.Count;
                nodes.Add(new Node(step.To, cost, estimate, current, step.Heading, step, placed));
                open.Enqueue(nodes.Count - 1, new Priority(cost + estimate, estimate));
            }
        }

        logger.LogDebug("No path from {Start} to {Goal} after {Expanded} positions", origin, label, expanded);

        return null;
    }

    /// <summary>
    /// Whether the search has had its time.
    /// </summary>
    /// <param name="best">Where the part of the way it ends with goes to, or null for nothing.</param>
    private bool IsOutOfTime(TimeSpan elapsed, int expanded, PartialRoutes partial, List<Node> nodes, out int? best)
    {
        best = null;

        if (elapsed >= _options.TimeLimit || expanded >= _options.MaxExpandedPositions)
        {
            best = partial.Best(nodes);

            return true;
        }

        // Past its patience a search settles for part of the way, if there is
        // a part worth having. Until then it keeps looking for all of it.
        if (elapsed >= _options.Patience && partial.Best(nodes) is { } found)
        {
            best = found;

            return true;
        }

        return false;
    }

    /// <summary>The positions of the route being replaced, if there is one.</summary>
    private static HashSet<Cell>? Favoured(Route? previous)
    {
        if (previous is null || previous.Moves.Count == 0)
            return null;

        var favoured = new HashSet<Cell>(previous.Moves.Count);

        foreach (var move in previous.Moves)
            favoured.Add(move.To);

        return favoured;
    }

    /// <summary>
    /// The least this could possibly still cost, in ticks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The horizontal distance is priced as if every block of it were a plain
    /// walk, which is the cheapest thing the player can do per block. Nothing
    /// the search can plan beats it by much: a jump covers more ground per move
    /// but costs far more than the walks it replaces, and so does a drop.
    /// </para>
    /// <para>
    /// The distance is measured the way the player is allowed to move, taking
    /// the diagonal part of the journey corner to corner. Counting those as two
    /// blocks each would overestimate, and an A* whose guess is too high stops
    /// being the cheapest route and becomes merely a route.
    /// </para>
    /// <para>
    /// Height still to climb is added on top, because every block of it takes a
    /// step up at the least. Without it, a goal on a hill looks as close as one
    /// at the foot of it, and the search spreads out around the bottom before it
    /// tries going up.
    /// </para>
    /// <para>
    /// Height still to lose is added as well, for the same reason the other way
    /// round: ore fifteen blocks down looks no further from any place on the
    /// surface above it than from the bottom of a shaft. Going down is cheap
    /// where there is a slope or a drop, so without digging this is only a
    /// little; with digging, it is what the cheapest block dug down costs, a
    /// guess rather than a bound. Where there is a cheaper way down after all,
    /// taking it gets closer faster, so the search still finds it first.
    /// </para>
    /// </remarks>
    private static double Heuristic(Cell from, Target target, bool diagonals, double perBlockDown)
    {
        var dx = Math.Abs(from.X - target.Position.X);
        var dz = Math.Abs(from.Z - target.Position.Z);

        double horizontal;

        if (diagonals)
        {
            // Every block of the shorter leg can be walked off as part of a
            // diagonal, at the cost of one corner-to-corner step rather than two
            // straight ones.
            var diagonal = Math.Min(dx, dz);

            horizontal = dx + dz - 2 * diagonal + diagonal * Math.Sqrt(2);
        }
        else
        {
            horizontal = dx + dz;
        }

        var climb = Math.Max(0, target.Position.Y - from.Y - target.VerticalSlack);
        var descent = Math.Max(0, from.Y - target.Position.Y - target.DropSlack);

        return Math.Max(0, horizontal - target.Slack) * Costs.Walk + climb * Costs.Climb + descent * perBlockDown;
    }

    /// <summary>
    /// The block the player is standing on, worked out from where it actually
    /// is rather than from which block its middle happens to be over.
    /// </summary>
    /// <remarks>
    /// At the lip of a drop the centre is already past the edge while the feet
    /// are still on the ledge behind. Reading that as "in mid-air above the
    /// floor below" puts the search three blocks lower than the player really
    /// is, and every route it plans from there is wrong.
    /// </remarks>
    private Cell StandingBlockFor(SearchContext context, Vector3d position)
    {
        Cell feet = position.ToBlockPosition();

        if (context.CanBeAt(feet))
            return feet;

        // On top of something a little short of a full block, such as a path
        // or soul sand: the feet are inside it, and the search thinks of the
        // player as standing on it.
        if (context.IsSolid(feet) && context.CanStandAt(feet.Above))
            return feet.Above;

        // Straddling an edge: whichever of the blocks under the player's box
        // holds it up is where it is really standing.
        foreach (var column in PlayerHitbox.ColumnsUnder(position))
        {
            var candidate = new Cell(column.X, feet.Y, column.Z);

            if (candidate != feet && context.CanStandAt(candidate))
            {
                logger.LogDebug("Standing on {Candidate}, not {Feet}, which its middle only hangs over", candidate, feet);

                return candidate;
            }
        }

        // Under water: the way anywhere starts with coming up for air.
        if (context.IsWater(feet))
        {
            for (var rise = 1; rise <= HeightSearchRange; rise++)
            {
                var surface = feet with { Y = feet.Y + rise };

                if (context.CanBeAt(surface))
                    return surface;

                if (!context.IsWater(surface))
                    break;
            }
        }

        // Nothing underneath anywhere, so it is on its way down.
        return StandingPositionFor(context, feet);
    }

    /// <summary>
    /// Where a player with nothing under it is going to end up.
    /// </summary>
    /// <remarks>
    /// Straight down, because a player whose feet are over nothing is on its way
    /// to the floor below whether it meant to be or not. Falling further than it
    /// can survive is not something to plan around, so the search is left to
    /// start where it was told and fail honestly.
    /// </remarks>
    private static Cell StandingPositionFor(SearchContext context, Cell position)
    {
        for (var drop = 1; drop <= Dropping.MaxFallHeight; drop++)
        {
            var below = position with { Y = position.Y - drop };

            if (context.CanStandAt(below))
                return below;

            if (!context.IsPassable(below))
                break;
        }

        return position;
    }

    /// <summary>
    /// The furthest block along the way to somewhere unknown that the client can
    /// actually see and stand on.
    /// </summary>
    /// <remarks>
    /// Walks the straight line back from the goal, taking the first place that
    /// is known and standable. Heights near the player's own are tried first,
    /// because that is where the ground tends to be for anything it can already
    /// see.
    /// </remarks>
    private static Cell? NearestKnownTowards(SearchContext context, Cell start, Cell goal)
    {
        var steps = Math.Max(Math.Abs(goal.X - start.X), Math.Abs(goal.Z - start.Z));

        if (steps == 0)
            return null;

        for (var step = steps; step > 0; step--)
        {
            var along = (double)step / steps;

            var x = (int)Math.Round(start.X + (goal.X - start.X) * along);
            var z = (int)Math.Round(start.Z + (goal.Z - start.Z) * along);

            for (var offset = 0; offset <= HeightSearchRange; offset++)
            {
                if (context.CanStandAt(new Cell(x, start.Y + offset, z)))
                    return new Cell(x, start.Y + offset, z);

                if (offset > 0 && context.CanStandAt(new Cell(x, start.Y - offset, z)))
                    return new Cell(x, start.Y - offset, z);
            }
        }

        return null;
    }

    private static Route Reconstruct(List<Node> nodes, int goal, bool reachesGoal, Cell origin, bool truncated)
    {
        var moves = new List<Move>();

        for (var at = goal; nodes[at].Parent >= 0; at = nodes[at].Parent)
            moves.Add(nodes[at].Arrival.ToMove());

        moves.Reverse();

        return new Route(moves, reachesGoal && !truncated, origin.ToVector3i(), truncated);
    }

    /// <summary>
    /// Where a search is headed, and how much leeway arriving has.
    /// </summary>
    /// <param name="Position">Where arriving happens.</param>
    /// <param name="Slack">
    /// How far short of <paramref name="Position"/>, sideways, arriving can
    /// already be, so that the estimate stays below the real cost.
    /// </param>
    /// <param name="VerticalSlack">The same, for how far below it.</param>
    /// <param name="DropSlack">The same, for how far above it.</param>
    private readonly record struct Target(Cell Position, double Slack, int VerticalSlack, int DropSlack);

    /// <summary>
    /// A place in the search: a block being stood on, the cheapest known way
    /// to it, and the move that way ends with.
    /// </summary>
    /// <param name="Position">The block being stood on.</param>
    /// <param name="Cost">The cheapest way here found so far.</param>
    /// <param name="Estimate">What the rest is guessed to cost, worked out once.</param>
    /// <param name="Parent">The node this way comes from, or -1 for the start.</param>
    /// <param name="Heading">Which way the player was going when it arrived, for pricing a turn.</param>
    /// <param name="Arrival">The move that got it here.</param>
    /// <param name="Placed">How many blocks the way here places.</param>
    private record struct Node(Cell Position, double Cost, double Estimate, int Parent, int Heading, Step Arrival, int Placed)
    {
        /// <summary>Whether the cheapest way here is settled and the node has been looked beyond.</summary>
        public bool Closed { get; set; }
    }

    /// <summary>
    /// Which node to look at next: the lowest total, and among equal totals the
    /// one closest to the goal.
    /// </summary>
    /// <remarks>
    /// On open ground a great many positions tie on total, and without the
    /// second rule the search looks at all of them. Preferring the one nearer
    /// the goal has it follow one of them to the end instead.
    /// </remarks>
    private readonly record struct Priority(double Total, double Estimate)
    {
        public static IComparer<Priority> Comparer { get; } = Comparer<Priority>.Create(static (left, right) =>
        {
            // Totals built up along different ways are not added up in the same
            // order, so two that ought to be equal can differ in the last
            // digit. That is not a difference worth ordering by.
            const double tolerance = 1e-9;

            if (Math.Abs(left.Total - right.Total) > tolerance)
                return left.Total.CompareTo(right.Total);

            return left.Estimate.CompareTo(right.Estimate);
        });
    }

    /// <summary>
    /// The most promising places the search has reached, for when it has to
    /// stop before it gets to the goal.
    /// </summary>
    private sealed class PartialRoutes(Cell origin)
    {
        private readonly int[] _best = Enumerable.Repeat(-1, _partialWeights.Length).ToArray();
        private readonly double[] _score = Enumerable.Repeat(double.PositiveInfinity, _partialWeights.Length).ToArray();

        public void Consider(int index, in Node node)
        {
            for (var i = 0; i < _partialWeights.Length; i++)
            {
                var score = node.Estimate + node.Cost / _partialWeights[i];

                if (score < _score[i])
                {
                    _score[i] = score;
                    _best[i] = index;
                }
            }
        }

        /// <summary>
        /// The most careful pick that still gets the player far enough to be
        /// worth it, or null if none does.
        /// </summary>
        public int? Best(List<Node> nodes)
        {
            foreach (var best in _best)
            {
                if (best < 0)
                    continue;

                var position = nodes[best].Position;
                var dx = position.X - origin.X;
                var dy = position.Y - origin.Y;
                var dz = position.Z - origin.Z;

                if (dx * dx + dy * dy + dz * dz > MinimumProgress * MinimumProgress)
                    return best;
            }

            return null;
        }
    }
}
