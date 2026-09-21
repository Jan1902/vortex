using Vortex.Modules.Player.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Player;

/// <summary>
/// Builds the plans for the movements the bot can make.
/// </summary>
/// <remarks>
/// <para>
/// This is where the knowledge of how a movement goes lives: how late to jump,
/// when to stop pushing, what counts as having made it. The controller only runs
/// what it is given, so adding a kind of movement is adding a method here rather
/// than a branch there.
/// </para>
/// <para>
/// The timings are asked of the physics rather than written down. A jump is
/// taken on the first tick it would come down squarely on the block it is aimed
/// at, and the direction is let go of on the first tick the player no longer
/// needs it -- both worked out by playing the rest of the movement out against
/// the world. That is what lets one rule cover a hop across a ditch and a
/// sprinting leap over four blocks, where a constant measured against one of
/// them would quietly be wrong for the other.
/// </para>
/// </remarks>
internal class MovementPlans(MovementSimulator simulator)
{
    /// <summary>How close to the target counts as having arrived.</summary>
    private const double ArrivalTolerance = 0.2;

    /// <summary>
    /// How far below the block it was aimed at the player may come down and
    /// still count as having made it.
    /// </summary>
    private const double LandingDropTolerance = 0.2;

    /// <summary>
    /// How far short of the block's centre a jump may come down and still be
    /// worth taking. Anything less is a landing on the near lip, which is across
    /// the gap but with nothing to spare.
    /// </summary>
    private const double SafeLandingMargin = 0.2;

    /// <summary>
    /// Half a block: the distance from a block's centre to its edge, near or far.
    /// </summary>
    private const double NearEdge = 0.5;

    /// <summary>
    /// How long a jump may take, in ticks. Only used to bound the simulations
    /// that time one; no arc comes close to it.
    /// </summary>
    private const int FlightTimeout = 60;

    /// <summary>
    /// Walks to a point on the level the player is already on.
    /// </summary>
    /// <remarks>
    /// Nothing here is axis-aligned. A direction is a direction, which is what
    /// lets a route plan a diagonal without this having to learn what one is.
    /// </remarks>
    /// <param name="origin">Where the walk starts.</param>
    /// <param name="target">Where to end up.</param>
    /// <param name="mode">How to move.</param>
    /// <param name="autoJump">
    /// Whether to try one jump at whatever gets in the way. Reactive, unlike a
    /// leap: it fires on the bump rather than before it, which only helps
    /// against something to climb and never against a gap.
    /// </param>
    public static MovementPlan Walk(
        Vector3d origin,
        Vector3d target,
        MovementMode mode = MovementMode.Walk,
        bool autoJump = false)
    {
        var heading = Normalize(target - origin);

        return MovementPlan.Single(
            MovementPhase.Steer(
                heading,
                Arrived(target, heading),
                mode,
                autoJump ? When.Blocked : null),
            target);
    }

    /// <summary>
    /// Walks onto a block one higher.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A step up is not a small jump, it is a jump taken from a standstill, and
    /// that is what makes it land on the block rather than over it. Walking into
    /// the block first is the whole trick: the collision takes the horizontal
    /// speed away, so the jump that follows goes up rather than along, and the
    /// little air control there is nudges the player onto the top.
    /// </para>
    /// <para>
    /// Which is why the jump fires on the bump rather than at a point. Anything
    /// under <see cref="PlayerPhysics.StepHeight"/> is climbed by the physics
    /// without a jump at all and never bumps into anything, so the plan has to
    /// be able to end either way.
    /// </para>
    /// </remarks>
    /// <param name="origin">Where the step starts.</param>
    /// <param name="target">The centre of the block to end up on.</param>
    /// <param name="mode">How to move.</param>
    public static MovementPlan StepUp(Vector3d origin, Vector3d target, MovementMode mode = MovementMode.Walk)
    {
        var heading = Normalize(target - origin);

        return new MovementPlan(
            [MovementPhase.Steer(heading, Arrived(target, heading), mode, When.Blocked)],
            target,
            When.All(When.Landed, When.AtOrAbove(target.Y - LandingDropTolerance)));
    }

    /// <summary>
    /// Steps off an edge and rides the fall down.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The pushing stops before the edge, not at it. A player who walks off at
    /// full speed keeps that speed all the way down, and the further the fall
    /// the further it carries -- a three block drop lands a whole block past the
    /// one it was aimed at. Letting go early enough that friction has taken some
    /// of the speed off is what puts the player on the right block.
    /// </para>
    /// <para>
    /// Early enough is not a distance, though: it depends on the speed, the
    /// height and where the edge is. So it is asked of the physics, by letting
    /// go in a simulation and seeing where that ends up -- the same question the
    /// middle of a jump asks, at the other end of the movement.
    /// </para>
    /// </remarks>
    /// <param name="origin">Where the walk to the edge starts.</param>
    /// <param name="target">The centre of the block to land on.</param>
    /// <param name="mode">How to walk up to the edge.</param>
    public MovementPlan Drop(Vector3d origin, Vector3d target, MovementMode mode = MovementMode.Walk)
    {
        var heading = Normalize(target - origin);

        var coast = new MovementPlan(
            [MovementPhase.Coast(When.TouchedDown)], target, LandsOnTheBlock(target, heading), FlightTimeout);

        return new MovementPlan(
        [
            // Up to the edge. Stepping up on the way would climb the very thing
            // the drop is meant to go round, and going over the edge still
            // pushing is the fallback rather than the plan.
            MovementPhase.Steer(
                heading,
                When.Any(LettingGoWouldLandIt(coast), When.Airborne),
                mode,
                allowStepUp: false),

            MovementPhase.Coast(When.TouchedDown),
        ],
            target,
            GotAcross(target, heading));
    }

    /// <summary>
    /// Runs up to an edge, jumps from it and comes down across a gap.
    /// </summary>
    /// <remarks>
    /// Three phases, which is the whole of what a jump is: get up to speed, go
    /// and hold the direction while the momentum is still worth adding to, then
    /// let go so the arc ends on the block it was aimed at rather than a block
    /// past it.
    /// </remarks>
    /// <param name="origin">Where the run-up starts, which fixes the direction.</param>
    /// <param name="takeOff">
    /// The edge to leave the ground at. The last resort rather than the plan:
    /// the jump goes as soon as it would land well, and only falls back to
    /// leaving here if that moment never came.
    /// </param>
    /// <param name="landing">The centre of the block to come down on.</param>
    /// <param name="mode">How to run up.</param>
    public MovementPlan Leap(Vector3d origin, Vector3d takeOff, Vector3d landing, MovementMode mode = MovementMode.Walk)
    {
        var heading = Normalize(landing - origin);

        // Two standards, and the difference between them is the margin the jump
        // is taken with. Deciding to go asks for a landing squarely on the block
        // it is aimed at; judging the jump afterwards accepts anything that got
        // across, because by then there is nothing to be done about it anyway.
        var landsWell = LandsOnTheBlock(landing, heading);
        var cleared = GotAcross(landing, heading);

        var coast = new MovementPlan([MovementPhase.Coast(When.TouchedDown)], landing, landsWell, FlightTimeout);

        // Stepping up is off for the whole leap, run-up included: catching the
        // lip of the gap would turn a jump that was planned into a scramble
        // that was not.
        MovementPhase[] arc =
        [
            // Off the ground, still pushing. Air control very nearly cancels
            // drag, so holding the direction is what keeps the speed up.
            MovementPhase.Steer(
                heading,
                When.Any(MomentumCarries(coast), When.TouchedDown),
                mode,
                When.Now,
                allowStepUp: false),

            // Letting go is the only say there is in where the jump ends.
            MovementPhase.Coast(When.TouchedDown),
        ];

        // The same arc, played out rather than flown, is what answers "is this
        // the tick to go on". Asking the movement itself means the answer cannot
        // drift away from what the movement then does.
        var ifItWentNow = new MovementPlan(arc, landing, landsWell, FlightTimeout);

        return new MovementPlan(
        [
            MovementPhase.Steer(
                heading,
                When.Any(ShouldTakeOff(ifItWentNow, heading, mode), When.PastPoint(takeOff, heading)),
                mode,
                allowStepUp: false),

            .. arc
        ],
            landing,
            cleared);
    }

    /// <summary>
    /// Whether this is the tick to go on: the jump lands on the block it is
    /// aimed at from here, and would not from where the next tick puts the
    /// player.
    /// </summary>
    /// <remarks>
    /// Jumping late is what gets the player across -- the whole arc is spent
    /// over the gap rather than half of it over ground already behind, and the
    /// player spends less of the run-up in the air, where a low ceiling nobody
    /// planned for is waiting. So the run-up carries on for as long as it can
    /// afford to, and running out of ground counts as not being able to afford
    /// another tick.
    /// </remarks>
    private Func<MovementState, bool> ShouldTakeOff(MovementPlan ifItWentNow, Vector3d heading, MovementMode mode)
        => state =>
        {
            if (!state.OnGround || !simulator.Works(state, ifItWentNow))
                return false;

            var running = new MovementInput(heading, mode, Jump: false, AllowStepUp: false);
            var next = simulator.Ahead(state, running);

            return !next.OnGround || !simulator.Works(next, ifItWentNow);
        };

    /// <summary>
    /// Whether what the player has already got will carry it the rest of the way
    /// on its own.
    /// </summary>
    /// <remarks>
    /// Asked by letting go in a simulation and seeing where that lands, because
    /// the answer depends on how fast the player is going, how far it still has
    /// to fall, what is in the way and how far there is left to go, and none of
    /// those is a constant.
    /// </remarks>
    private Func<MovementState, bool> MomentumCarries(MovementPlan coast)
        => state => !state.OnGround && simulator.Works(state, coast);

    /// <summary>
    /// Whether letting go right now would put the player on the block it is
    /// aimed at.
    /// </summary>
    /// <remarks>
    /// Asked from the ground as well as from the air, which is the difference
    /// between this and <see cref="MomentumCarries"/>. A fall is not committed
    /// to at an edge the way a jump is: the last say in where it ends is had
    /// before the ground runs out, by stopping early enough for friction to take
    /// some of the speed off.
    /// </remarks>
    private Func<MovementState, bool> LettingGoWouldLandIt(MovementPlan coast)
        => state => simulator.Works(state, coast);

    /// <summary>
    /// Whether a movement aimed at a point has got there.
    /// </summary>
    /// <remarks>
    /// Close enough, or past it: at sprinting speed a single tick covers a
    /// quarter of a block, so a movement that insisted on landing inside the
    /// tolerance would step over it and walk on.
    /// </remarks>
    private static Func<MovementState, bool> Arrived(Vector3d target, Vector3d heading)
        => When.Any(When.Within(target, ArrivalTolerance), When.PastPoint(target, heading));

    /// <summary>
    /// Whether the player came down on the block a leap was aimed at, and not
    /// past it.
    /// </summary>
    /// <remarks>
    /// This is what a jump is planned to do, and it is a window rather than a
    /// threshold on purpose. Without a far edge, landing further is always
    /// better, every moment to take off is as good as the next, and "as late as
    /// possible" collapses into leaving the ground with nothing to spare. With
    /// one, there is a stretch of run-up that works, and the jump can be taken
    /// at the end of it.
    /// </remarks>
    private static Func<MovementState, bool> LandsOnTheBlock(Vector3d landing, Vector3d heading)
        => When.All(
            GotAcross(landing, heading),
            When.PastPoint(landing - heading * SafeLandingMargin, heading),
            When.Not(When.PastPoint(landing + heading * NearEdge, heading)));

    /// <summary>
    /// Whether the player got across, which is all that can be asked of a jump
    /// once it is over.
    /// </summary>
    /// <remarks>
    /// Measured along the direction of travel, so overshooting counts: a jump is
    /// ballistic, there is no making it shorter once it has left the ground, and
    /// coming down beyond where it was aimed at the right level is still across.
    /// Half a block short of the centre is the block's near edge, which is far
    /// enough along to be standing on it.
    /// </remarks>
    private static Func<MovementState, bool> GotAcross(Vector3d landing, Vector3d heading)
        => When.All(
            When.Landed,
            When.PastPoint(landing - heading * NearEdge, heading),
            When.AtOrAbove(landing.Y - LandingDropTolerance));

    private static Vector3d Normalize(Vector3d direction)
    {
        var length = Math.Sqrt(direction.X * direction.X + direction.Z * direction.Z);

        return length < 1e-6
            ? Vector3d.Zero
            : new Vector3d(direction.X / length, 0, direction.Z / length);
    }
}
