using Microsoft.Extensions.Logging;
using Vortex.Modules.Navigation.Abstraction;
using Vortex.Modules.World.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Navigation;

/// <summary>
/// A plain A* search over block positions.
/// </summary>
/// <remarks>
/// <para>
/// A node is a block position the player's feet can occupy: two free blocks with
/// something solid underneath. From there it walks to the blocks around it --
/// corner to corner as well as along the axes, where that is allowed -- steps up
/// one, drops down a few, or jumps a gap, which may land level, a block higher or
/// a couple lower.
/// </para>
/// <para>
/// What it will actually plan is decided by the capabilities it is handed, not
/// by what it can imagine. There are still no doors, no swimming, no ladders, no
/// placing or breaking blocks to get through, and no notion of danger beyond
/// what hurts to stand in. Each of those is its own piece of work.
/// </para>
/// </remarks>
internal class AStarPathfinder(IWorldManager world, ILogger<AStarPathfinder> logger) : IPathfinder
{
    /// <summary>
    /// How many nodes may be expanded before the search gives up. This is what
    /// stops a search for an unreachable goal from walking the whole of the
    /// loaded world.
    /// </summary>
    /// <remarks>
    /// Counted in nodes rather than positions, and a position is as many nodes
    /// as there are ways into it, because the direction it was reached from is
    /// part of it. The budget is sized for that, so that neither the turn cost
    /// nor the diagonals quietly shrank how far the search is willing to look
    /// before reporting no way through.
    /// </remarks>
    private const int MaxExpandedNodes = 80_000;

    /// <summary>
    /// How far the player is willing to drop in one step.
    /// </summary>
    /// <remarks>
    /// Three blocks is the most that costs no health. Going further needs
    /// something to break the fall, which is a move of its own rather than a
    /// bigger number here.
    /// </remarks>
    private const int MaxFallHeight = 3;

    /// <summary>
    /// What one block of walking costs, in ticks.
    /// </summary>
    /// <remarks>
    /// Everything the search prices is measured in ticks, because as soon as it
    /// can mine or bridge its way through, it has to answer "round or through",
    /// and that is a question about time. Mixing a count of blocks with a
    /// mining time in one number would have it tunnel through a mountain
    /// because that came out three blocks shorter.
    /// </remarks>
    private const double WalkCost = 1 / 0.2158;

    /// <summary>
    /// What getting onto a block one higher costs, in ticks.
    /// </summary>
    /// <remarks>
    /// Measured from the physics: walking into the block, jumping, and coming
    /// down on top of it takes about eight ticks from the moment of take-off.
    /// Nearly twice a plain walk, which is what makes the search prefer a flat
    /// way round when there is one going spare.
    /// </remarks>
    private const double StepUpCost = 9.0;

    /// <summary>What stepping off an edge costs before the fall itself.</summary>
    private const double DropCost = 5.0;

    /// <summary>
    /// What a jump across a gap costs, in ticks: the arc itself, plus a little
    /// for the run-up it has to be taken at.
    /// </summary>
    private const double JumpGapCost = 14.0;

    /// <summary>
    /// What a jump taken at a sprint costs.
    /// </summary>
    /// <remarks>
    /// Dearer than the same jump walked, though it takes no longer. The extra is
    /// not time, it is margin: the faster the take-off, the less say there is in
    /// where the player comes down, so a route that sprints where it could have
    /// walked is taking a risk for nothing.
    /// </remarks>
    private const double SprintJumpCost = 18.0;

    /// <summary>Charged per block of gap, so the shortest jump that works wins.</summary>
    private const double JumpBlockCost = 2.0;

    /// <summary>
    /// Charged per block of the fall beyond the first. Falling accelerates, so
    /// each further block takes less time than the one before it; this is the
    /// flat approximation of that.
    /// </summary>
    private const double FallCostPerBlock = 2.0;

    /// <summary>
    /// Charged for changing direction, which is what makes the search prefer a
    /// few long legs to a staircase of the same length.
    /// </summary>
    /// <remarks>
    /// <para>
    /// With every direction costing exactly the same, a great many routes are
    /// tied on price and the search is free to return any of them, including one
    /// that zigzags across open ground. That costs nothing to walk, but it
    /// cannot be covered in one movement, so the bot ends up stopping and
    /// searching again at every single block.
    /// </para>
    /// <para>
    /// Small enough that it only ever settles a tie: it would take a thousand
    /// turns to outweigh one extra block, so no route is ever made longer in
    /// order to make it straighter. Written as a fraction of the walk so that it
    /// stays that way if the walk is ever re-measured.
    /// </para>
    /// </remarks>
    private const double TurnCost = WalkCost / 1000;

    /// <summary>How high above its feet the player's eyes are, which is where reach is measured from.</summary>
    private const double EyeHeight = 1.62;

    /// <summary>The direction of a node nothing has been walked into yet.</summary>
    private const int NoDirection = -1;

    /// <summary>
    /// How far above and below the player to look when picking somewhere to
    /// head for on the way to a goal that is not loaded yet.
    /// </summary>
    private const int HeightSearchRange = 4;

    /// <summary>
    /// The ways out of a block. The four along the axes come first, so that a
    /// search without diagonals can simply take the front of the list.
    /// </summary>
    private static readonly Vector3i[] _directions =
    [
        new(1, 0, 0),
        new(-1, 0, 0),
        new(0, 0, 1),
        new(0, 0, -1),

        new(1, 0, 1),
        new(1, 0, -1),
        new(-1, 0, 1),
        new(-1, 0, -1),
    ];

    /// <summary>How many of <see cref="_directions"/> run along an axis.</summary>
    private const int StraightDirections = 4;

    /// <summary>
    /// Where a jump can land that is neither along an axis nor straight across
    /// a corner, such as two blocks on and one to the side: every such offset
    /// within the furthest reach there is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Headings for these carry on after <see cref="_directions"/>, so that
    /// turning into or out of one costs a turn like any other change of way.
    /// </para>
    /// <para>
    /// Each comes with the neighbour its line leaves the block through -- always
    /// one along an axis, since the line never runs through a corner. A jump
    /// only makes sense where that neighbour is a gap; where it is floor, the
    /// player can walk on and jump from there. Ruling a jump out by that one
    /// block, which the search looks at anyway, is what keeps these from costing
    /// anything on ground where there is nothing to jump over.
    /// </para>
    /// </remarks>
    private static readonly (Vector3i Offset, int Exit)[] _offAxisJumps = OffAxisJumpOffsets().ToArray();

    public Route? FindRoute(Vector3d start, Vector3i goal, MovementCapabilities? capabilities = null)
        => Search(StandingBlockFor(start), goal, capabilities);

    public Route? FindRoute(Vector3i start, Vector3i goal, MovementCapabilities? capabilities = null)
        => Search(CanStandAt(start) ? start : StandingPositionFor(start), goal, capabilities);

    public Route? FindRouteWithinReach(Vector3d start, Vector3i target, double reach, MovementCapabilities? capabilities = null)
    {
        var origin = StandingBlockFor(start);

        // Not loaded yet: head that way as for any other unknown goal, and look
        // for somewhere to stand once it is known.
        if (world.GetBlock(target) is null)
            return Search(origin, target, capabilities);

        return Explore(
            origin,
            position => IsWithinReach(position, target, reach)
                && position != target
                && Above(position) != target,
            target,
            reach,
            capabilities ?? MovementCapabilities.Walking,
            reachesGoal: true,
            label: target);
    }

    public Route? FindRouteNear(Vector3d start, Vector3i target, int range, MovementCapabilities? capabilities = null)
    {
        var origin = StandingBlockFor(start);

        if (world.GetBlock(target) is null)
            return Search(origin, target, capabilities);

        return Explore(
            origin,
            position => IsNear(position, target, range),
            target,
            // The corner of the square is the furthest off the target an
            // arrival can be, and the estimate must not count that as still to go.
            range * Math.Sqrt(2),
            capabilities ?? MovementCapabilities.Walking,
            reachesGoal: true,
            label: target);
    }

    /// <summary>Whether a position is at most some blocks off a target sideways, and at most one up or down.</summary>
    public static bool IsNear(Vector3i position, Vector3i target, int range)
        => Math.Abs(position.X - target.X) <= range
        && Math.Abs(position.Z - target.Z) <= range
        && Math.Abs(position.Y - target.Y) <= 1;

    /// <summary>
    /// Whether a block is within reach of a player standing at a position,
    /// measured from the eyes of one standing in the middle of it.
    /// </summary>
    private static bool IsWithinReach(Vector3i standing, Vector3i target, double reach)
    {
        var dx = standing.X - target.X;
        var dy = standing.Y + EyeHeight - (target.Y + 0.5);
        var dz = standing.Z - target.Z;

        return dx * dx + dy * dy + dz * dz <= reach * reach;
    }

    private Route? Search(Vector3i origin, Vector3i goal, MovementCapabilities? capabilities)
    {
        var allowed = capabilities ?? MovementCapabilities.Walking;

        if (origin == goal)
            return new Route([], ReachesGoal: true, Origin: origin);

        // A goal the client has not been sent yet is not unreachable, it is
        // unknown: chunks arrive as the player approaches, so refusing to move
        // would mean never being able to walk further than the view distance.
        // Head for the furthest known ground along the way instead and ask
        // again from there.
        var reachesGoal = true;
        var destination = goal;

        if (world.GetBlock(goal) is null)
        {
            if (NearestKnownTowards(origin, goal) is not { } staging)
            {
                logger.LogDebug("No path towards {Goal}: nothing known in that direction", goal);

                return null;
            }

            logger.LogDebug("{Goal} is not loaded yet; heading for {Staging} first", goal, staging);

            destination = staging;
            reachesGoal = false;
        }

        if (!CanStandAt(destination))
        {
            logger.LogDebug("No path to {Goal}: nothing to stand on there", destination);

            return null;
        }

        return Explore(origin, position => position == destination, destination, 0, allowed, reachesGoal, goal);
    }

    /// <summary>
    /// The A* search itself, towards whatever counts as arriving.
    /// </summary>
    /// <param name="isGoal">Whether standing at a position is arriving.</param>
    /// <param name="towards">Where arriving happens, for the estimate of what is left.</param>
    /// <param name="slack">
    /// How far short of <paramref name="towards"/> arriving can already be, so
    /// that the estimate stays below the real cost.
    /// </param>
    /// <param name="label">What the search is for, in the log.</param>
    private Route? Explore(
        Vector3i origin,
        Func<Vector3i, bool> isGoal,
        Vector3i towards,
        double slack,
        MovementCapabilities allowed,
        bool reachesGoal,
        Vector3i label)
    {
        if (isGoal(origin))
            return new Route([], reachesGoal, Origin: origin);

        double Estimate(Vector3i from)
            => Math.Max(0, Heuristic(from, towards, allowed.Diagonals) - slack * WalkCost);

        // A node is a position together with the direction it was walked into
        // from, because what a turn costs depends on where the player came from.
        // The same position can therefore be reached as several nodes.
        var searchFrom = new Node(origin, NoDirection);

        var open = new PriorityQueue<Node, double>();

        // Each node remembers the node it came from and the move that got it
        // there, which is what the finished route is made of.
        var cameFrom = new Dictionary<Node, (Node From, Move Move)>();
        var costSoFar = new Dictionary<Node, double> { [searchFrom] = 0 };
        var settled = new HashSet<Node>();

        open.Enqueue(searchFrom, Estimate(origin));

        var expanded = 0;

        while (open.TryDequeue(out var current, out _))
        {
            if (isGoal(current.Position))
                return Reconstruct(cameFrom, searchFrom, current, reachesGoal, origin);

            // The queue has no decrease-key, so a node can sit in it more than
            // once. The first time it comes out carries its best cost, and any
            // later copy is stale.
            if (!settled.Add(current))
                continue;

            if (++expanded > MaxExpandedNodes)
            {
                logger.LogDebug("Gave up looking for a path to {Goal} after {Expanded} positions", label, expanded);

                return null;
            }

            foreach (var (move, stepCost, heading) in Steps(current.Position, allowed))
            {
                var next = new Node(move.To, heading);

                if (settled.Contains(next))
                    continue;

                var turning = current.Direction != NoDirection && current.Direction != heading;
                var cost = costSoFar[current] + stepCost + (turning ? TurnCost : 0);

                if (costSoFar.TryGetValue(next, out var best) && cost >= best)
                    continue;

                costSoFar[next] = cost;
                cameFrom[next] = (current, move);

                open.Enqueue(next, cost + Estimate(move.To));
            }
        }

        logger.LogDebug("No path from {Start} to {Goal}", origin, label);

        return null;
    }

    /// <summary>
    /// The moves available from one place, what each of them costs, and which
    /// way it went.
    /// </summary>
    /// <remarks>
    /// The move and its cost are produced together on purpose: what the search
    /// decided and what it paid for that decision are the same fact, and the
    /// route carries the move along so that nothing downstream has to work out
    /// from the geometry what was meant.
    /// </remarks>
    private IEnumerable<(Move Move, double Cost, int Heading)> Steps(Vector3i from, MovementCapabilities allowed)
    {
        var headings = allowed.Diagonals ? _directions.Length : StraightDirections;

        // Which of the neighbours along the axes are gaps, one bit per heading.
        var gaps = 0;

        for (var heading = 0; heading < headings; heading++)
        {
            var direction = _directions[heading];
            var diagonal = heading >= StraightDirections;

            // A corner cannot be squeezed through. The player is wider than a
            // point, so going round one means both blocks beside it have to be
            // out of the way, or it scrapes through geometry the server will not
            // let it through.
            if (diagonal && !CornerIsClear(from, direction))
                continue;

            var length = Length(direction);
            var side = Offset(from, direction);

            if (CanStandAt(side))
            {
                yield return (new Walk(side), WalkCost * length, heading);

                continue;
            }

            if (IsBlocked(side))
            {
                // Something is in the way at body height. Stepping onto it works
                // if its top is clear and there is room to jump from here.
                // Straight on only: a step up taken across a corner catches the
                // edge as often as it clears it.
                var up = Above(side);

                if (!diagonal && CanStandAt(up) && IsPassable(Above(Above(from))))
                    yield return (new StepUp(up), StepUpCost, heading);

                continue;
            }

            gaps |= 1 << heading;

            // The way is open but there is no floor: fall until something
            // catches the player, and give up if that is too far down.
            foreach (var drop in DropsFrom(side))
                yield return (drop.Move, drop.Cost * length, heading);

            if (allowed.JumpGaps && JumpAcross(from, direction, allowed.Sprint) is { } leap)
                yield return (leap.Move, leap.Cost, heading);
        }

        if (!allowed.JumpGaps || !allowed.Diagonals)
            yield break;

        for (var i = 0; i < _offAxisJumps.Length; i++)
        {
            var (offset, exit) = _offAxisJumps[i];

            if ((gaps & (1 << exit)) == 0)
                continue;

            if (JumpOffAxis(from, offset, allowed.Sprint) is { } leap)
                yield return (leap.Move, leap.Cost, _directions.Length + i);
        }
    }

    private static IEnumerable<(Vector3i Offset, int Exit)> OffAxisJumpOffsets()
    {
        var reach = (int)Math.Floor(JumpReach.Furthest(sprinting: true, JumpReach.LowestLanding));

        for (var dx = -reach; dx <= reach; dx++)
            for (var dz = -reach; dz <= reach; dz++)
            {
                if (dx == 0 || dz == 0 || Math.Abs(dx) == Math.Abs(dz) || dx * dx + dz * dz > reach * reach)
                    continue;

                // Out through the side the line meets first: the one across the
                // axis it covers more of.
                var exit = Math.Abs(dx) > Math.Abs(dz)
                    ? new Vector3i(Math.Sign(dx), 0, 0)
                    : new Vector3i(0, 0, Math.Sign(dz));

                yield return (new Vector3i(dx, 0, dz), Array.IndexOf(_directions, exit));
            }
    }

    /// <summary>
    /// A jump to a block that lies off every one of the eight directions, such
    /// as two on and one to the side, or null if it cannot be made.
    /// </summary>
    /// <remarks>
    /// Judged the way the player flies it: in a straight line from the middle
    /// of the block to the middle of the landing, so everything the player's
    /// box sweeps over on that line has to be clear, and the reach is the
    /// distance between the two middles. As with the other jumps, the highest
    /// landing wins and walking is preferred to sprinting wherever both reach.
    /// </remarks>
    private (Move Move, double Cost)? JumpOffAxis(Vector3i from, Vector3i offset, bool maySprint)
    {
        if (!IsPassable(Above(Above(from))))
            return null;

        var distance = Math.Sqrt(offset.X * offset.X + offset.Z * offset.Z);
        var gap = Math.Max(Math.Abs(offset.X), Math.Abs(offset.Z)) - 1;

        for (var rise = JumpReach.HighestLanding; rise >= JumpReach.LowestLanding; rise--)
        {
            var sprinting = distance > JumpReach.Furthest(sprinting: false, rise);

            if (sprinting && (!maySprint || distance > JumpReach.Furthest(sprinting: true, rise)))
                continue;

            var landing = new Vector3i(from.X + offset.X, from.Y + rise, from.Z + offset.Z);

            if (!CanStandAt(landing) || !CanFlyTo(from, landing))
                continue;

            return sprinting
                ? (new JumpGap(landing, gap, Sprinting: true), SprintJumpCost + gap * JumpBlockCost)
                : (new JumpGap(landing, gap), JumpGapCost + gap * JumpBlockCost);
        }

        return null;
    }

    /// <summary>
    /// Whether the straight flight from one block to a landing off to the side
    /// actually crosses a gap, and is clear all the way.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The gap comes first, because it is cheap to ask and on open ground the
    /// answer is always no: where every block under the line could be walked
    /// on, there is nothing to jump over and walking gets there anyway.
    /// </para>
    /// <para>
    /// Then every column the player's box passes over has to be clear at body
    /// height and the block above, as for any jump.
    /// </para>
    /// </remarks>
    private bool CanFlyTo(Vector3i from, Vector3i landing)
    {
        const int samples = 20;

        var start = new Vector3d(from.X + 0.5, from.Y, from.Z + 0.5);
        var end = new Vector3d(landing.X + 0.5, from.Y, landing.Z + 0.5);
        var landingColumn = new Vector2i(landing.X, landing.Z);

        Vector3d Along(int sample)
            => start + (end - start) * ((double)sample / samples);

        var underLine = Enumerable.Range(1, samples - 1)
            .Select(sample => Along(sample).ToBlockPosition())
            .Where(block => block != from && new Vector2i(block.X, block.Z) != landingColumn)
            .Distinct();

        if (underLine.All(CanStandAt))
            return false;

        var passedOver = new HashSet<Vector2i>();

        for (var sample = 0; sample <= samples; sample++)
        {
            foreach (var column in PlayerHitbox.ColumnsUnder(Along(sample)))
            {
                if (!passedOver.Add(column))
                    continue;

                var over = new Vector3i(column.X, from.Y, column.Z);

                if (over == from)
                    continue;

                // Jumping up onto a landing, its column at take-off height is
                // the very block landed on.
                if (column == landingColumn && landing.Y > from.Y)
                    continue;

                if (IsBlocked(over) || !IsPassable(Above(Above(over))))
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Stepping off an edge, where the floor next door is missing but something
    /// below it is not.
    /// </summary>
    /// <remarks>
    /// Any floor will do, a single block with nothing beyond it included: the
    /// movement lets go before the edge and brakes in the air, so it comes down
    /// on the block it was aimed at rather than one further on.
    /// </remarks>
    /// <returns>At most one drop: the first floor the player would meet.</returns>
    private IEnumerable<(Move Move, double Cost)> DropsFrom(Vector3i side)
    {
        for (var drop = 1; drop <= MaxFallHeight; drop++)
        {
            var landing = new Vector3i(side.X, side.Y - drop, side.Z);

            if (CanStandAt(landing))
            {
                yield return (new Drop(landing, drop), DropCost + drop * FallCostPerBlock);
                yield break;
            }

            if (!IsPassable(landing))
                yield break;
        }
    }

    /// <summary>
    /// Whether a diagonal step has room to go round the corner.
    /// </summary>
    /// <remarks>
    /// Both of the blocks either side of the corner have to be clear at body
    /// height. Standing on them is not required -- cutting across the corner of
    /// a hole is exactly what a diagonal is for -- but squeezing between two
    /// walls is not.
    /// </remarks>
    private bool CornerIsClear(Vector3i at, Vector3i direction)
        => !IsBlocked(Offset(at, new Vector3i(direction.X, 0, 0)))
        && !IsBlocked(Offset(at, new Vector3i(0, 0, direction.Z)));

    /// <summary>
    /// The cheapest jump over a gap in this direction, or null if there is
    /// nothing worth jumping to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The landing does not have to be level. Jumping up onto a ledge across a
    /// gap and coming down onto one below are the same movement with the same
    /// timing; what changes is how far it carries, which is what
    /// <see cref="JumpReach"/> knows.
    /// </para>
    /// <para>
    /// The nearest landing wins, and walking is preferred to sprinting wherever
    /// both reach: the slower the take-off, the more say there is in where it
    /// comes down.
    /// </para>
    /// </remarks>
    private (Move Move, double Cost)? JumpAcross(Vector3i from, Vector3i direction, bool maySprint)
    {
        // Something to jump over. A floor next door is a walk, and a wall is a
        // step up; neither is this.
        if (CanStandAt(Offset(from, direction)) || IsBlocked(Offset(from, direction)))
            return null;

        // Room to get off the ground in the first place.
        if (!IsPassable(Above(Above(from))))
            return null;

        var length = Length(direction);
        var reach = JumpReach.Furthest(maySprint, JumpReach.LowestLanding);

        for (var steps = 2; steps * length <= reach; steps++)
        {
            // Everything flown over has to be clear, head height and all: the
            // player rises more than a block on the way across, so a ceiling
            // turns a jump into a bang on the head and a fall into the gap.
            if (!CanFlyOver(Offset(from, direction * (steps - 1)), direction))
                return null;

            if (LandingAt(from, direction, steps, maySprint) is { } landing)
                return landing;
        }

        return null;
    }

    /// <summary>
    /// Whether the player would pass through a block on the way across without
    /// catching anything.
    /// </summary>
    private bool CanFlyOver(Vector3i over, Vector3i direction)
        => !IsBlocked(over)
        && IsPassable(Above(Above(over)))
        && (direction.X == 0 || direction.Z == 0 || CornerIsClear(over, direction));

    /// <summary>
    /// The best landing a given number of blocks out, at whatever height the
    /// jump can be aimed at.
    /// </summary>
    /// <remarks>
    /// Highest first, because coming down onto a ledge is worth more than
    /// dropping past it, and a lower landing is still available from there as a
    /// plain fall.
    /// </remarks>
    private (Move Move, double Cost)? LandingAt(Vector3i from, Vector3i direction, int steps, bool maySprint)
    {
        var distance = steps * Length(direction);
        var across = Offset(from, direction * steps);

        for (var rise = JumpReach.HighestLanding; rise >= JumpReach.LowestLanding; rise--)
        {
            var landing = new Vector3i(across.X, from.Y + rise, across.Z);

            if (!CanStandAt(landing))
                continue;

            if (distance <= JumpReach.Furthest(sprinting: false, rise))
                return (new JumpGap(landing, steps - 1), JumpGapCost + (steps - 1) * JumpBlockCost);

            if (maySprint && distance <= JumpReach.Furthest(sprinting: true, rise))
                return (new JumpGap(landing, steps - 1, Sprinting: true),
                    SprintJumpCost + (steps - 1) * JumpBlockCost);
        }

        return null;
    }

    /// <summary>How far a step in a direction actually covers, in blocks.</summary>
    private static double Length(Vector3i direction)
        => direction.X != 0 && direction.Z != 0 ? Math.Sqrt(2) : 1;

    /// <summary>
    /// Whether the player fits at a position and would survive being there: two
    /// blocks of room, something solid to stand on, and nothing that hurts.
    /// </summary>
    private bool CanStandAt(Vector3i position)
        => IsPassable(position)
        && IsPassable(Above(position))
        && !IsPassable(Below(position))
        && IsSafeAt(position);

    /// <summary>
    /// Whether standing at a position would damage the player.
    /// </summary>
    /// <remarks>
    /// Checked on the two blocks the body occupies and the one it stands on,
    /// because magma burns through boots while lava and fire burn what is in
    /// them. Drowning is asked about head height only, so that wading through
    /// something shallow stays allowed.
    /// </remarks>
    private bool IsSafeAt(Vector3i position)
    {
        var head = world.GetBlock(Above(position));

        return !BlockHazard.IsHarmful(world.GetBlock(position))
            && !BlockHazard.IsHarmful(head)
            && !BlockHazard.IsHarmful(world.GetBlock(Below(position)))
            && !BlockHazard.Drowns(head);
    }

    /// <summary>Whether a position is obstructed at either body height.</summary>
    private bool IsBlocked(Vector3i position)
        => !IsPassable(position) || !IsPassable(Above(position));

    private bool IsPassable(Vector3i position)
        => !BlockCollision.IsSolid(world.GetBlock(position));

    /// <summary>
    /// The least this could possibly still cost, in ticks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Horizontal distance only, priced as if every block of it were a plain
    /// walk, which is the cheapest thing the player can do per block. Nothing
    /// the search can plan beats it: a jump covers more ground per move but
    /// costs far more than the walks it replaces, and so does a drop.
    /// </para>
    /// <para>
    /// The distance is measured the way the player is allowed to move, taking
    /// the diagonal part of the journey corner to corner. Counting those as two
    /// blocks each would overestimate, and an A* whose guess is too high stops
    /// being the cheapest route and becomes merely a route.
    /// </para>
    /// </remarks>
    private static double Heuristic(Vector3i from, Vector3i to, bool diagonals)
    {
        var dx = Math.Abs(from.X - to.X);
        var dz = Math.Abs(from.Z - to.Z);

        if (!diagonals)
            return (dx + dz) * WalkCost;

        // Every block of the shorter leg can be walked off as part of a
        // diagonal, at the cost of one corner-to-corner step rather than two
        // straight ones.
        var diagonal = Math.Min(dx, dz);

        return (dx + dz - 2 * diagonal + diagonal * Math.Sqrt(2)) * WalkCost;
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
    private Vector3i StandingBlockFor(Vector3d position)
    {
        var feet = position.ToBlockPosition();

        if (CanStandAt(feet))
            return feet;

        // Straddling an edge: whichever of the blocks under the player's box
        // holds it up is where it is really standing.
        foreach (var column in PlayerHitbox.ColumnsUnder(position))
        {
            var candidate = new Vector3i(column.X, feet.Y, column.Z);

            if (candidate != feet && CanStandAt(candidate))
            {
                logger.LogDebug("Standing on {Candidate}, not {Feet}, which its middle only hangs over", candidate, feet);

                return candidate;
            }
        }

        // Nothing underneath anywhere, so it is on its way down.
        return StandingPositionFor(feet);
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
    private Vector3i StandingPositionFor(Vector3i position)
    {
        for (var drop = 1; drop <= MaxFallHeight; drop++)
        {
            var below = new Vector3i(position.X, position.Y - drop, position.Z);

            if (CanStandAt(below))
                return below;

            if (!IsPassable(below))
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
    private Vector3i? NearestKnownTowards(Vector3i start, Vector3i goal)
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
                if (CanStandAt(new Vector3i(x, start.Y + offset, z)))
                    return new Vector3i(x, start.Y + offset, z);

                if (offset > 0 && CanStandAt(new Vector3i(x, start.Y - offset, z)))
                    return new Vector3i(x, start.Y - offset, z);
            }
        }

        return null;
    }

    private static Route Reconstruct(
        Dictionary<Node, (Node From, Move Move)> cameFrom,
        Node searchFrom,
        Node goal,
        bool reachesGoal,
        Vector3i origin)
    {
        var moves = new List<Move>();

        for (var node = goal; node != searchFrom;)
        {
            var (from, move) = cameFrom[node];

            moves.Add(move);
            node = from;
        }

        moves.Reverse();

        return new Route(moves, reachesGoal, origin);
    }

    private static Vector3i Offset(Vector3i position, Vector3i by)
        => new(position.X + by.X, position.Y + by.Y, position.Z + by.Z);

    private static Vector3i Above(Vector3i position)
        => new(position.X, position.Y + 1, position.Z);

    private static Vector3i Below(Vector3i position)
        => new(position.X, position.Y - 1, position.Z);

    /// <summary>
    /// A place in the search: the block being stood on, and which way the player
    /// was walking when it arrived there.
    /// </summary>
    /// <param name="Position">The block being stood on.</param>
    /// <param name="Direction">
    /// Index into the directions this search steps in, or
    /// <see cref="NoDirection"/> for the position the search started from.
    /// </param>
    private readonly record struct Node(Vector3i Position, int Direction);
}
