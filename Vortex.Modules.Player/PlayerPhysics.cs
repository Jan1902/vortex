using Vortex.Modules.Player.Abstraction;
using Vortex.Modules.World.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Player;

/// <summary>
/// Advances the player's position by one tick.
/// </summary>
/// <remarks>
/// This is a pure simulation: what goes in is a position, a velocity and an
/// input, what comes out is the resulting state. It never decides to jump, to
/// change direction or to stop. Those are decisions, and decisions live above
/// this class, which keeps it testable and lets a path finder ask "where would I
/// end up" without anything acting on the answer.
/// </remarks>
internal class PlayerPhysics(IWorldManager world)
{
    /// <summary>Blocks per tick squared that the player is pulled down by.</summary>
    public const double Gravity = 0.08;

    /// <summary>Factor applied to vertical velocity each tick.</summary>
    public const double VerticalDrag = 0.98;

    /// <summary>Air resistance applied to horizontal velocity each tick.</summary>
    public const double HorizontalDrag = 0.91;

    /// <summary>
    /// How much speed the player can add in mid-air each tick by holding a
    /// direction.
    /// </summary>
    /// <remarks>
    /// Far less than on the ground: in the air the player mostly keeps the
    /// momentum it took off with. At walking speed this almost exactly cancels
    /// <see cref="HorizontalDrag"/>, so a running jump carries on at the speed
    /// it started with, while a jump taken from a standstill barely moves
    /// forward at all -- which is what lands the player on top of the block it
    /// jumped at rather than beyond it.
    /// </remarks>
    public const double AirControl = 0.02;

    /// <summary>
    /// Friction of an ordinary block, applied on top of the air resistance while
    /// standing on it. Without it the player slides for metres after stopping, as
    /// though the whole world were ice.
    /// </summary>
    public const double GroundFriction = 0.6;

    /// <summary>Speed at which falling stops accelerating.</summary>
    public const double TerminalVelocity = 3.92;

    /// <summary>Upwards velocity of a jump, which carries the player 1.25 blocks high.</summary>
    public const double JumpVelocity = 0.42;

    /// <summary>How high an obstacle may be to be stepped onto rather than jumped over.</summary>
    public const double StepHeight = 0.6;

    /// <summary>Width of the player's bounding box, centred on its position.</summary>
    public const double Width = PlayerHitbox.Width;

    /// <summary>Height of the player's bounding box, measured up from its feet.</summary>
    public const double Height = PlayerHitbox.Height;

    /// <summary>Velocities below this are treated as standing still.</summary>
    private const double NegligibleVelocity = 0.003;

    private const double HalfWidth = Width / 2;
    private const double Epsilon = 0.0001;

    /// <summary>
    /// Speed in blocks per tick for each way of moving, matching the vanilla
    /// speeds of 4.317, 5.612 and 1.306 blocks per second.
    /// </summary>
    public static double SpeedOf(MovementMode mode)
        => mode switch
        {
            MovementMode.Sprint => 0.2806,
            MovementMode.Sneak => 0.0653,
            _ => 0.2158
        };

    /// <summary>
    /// Computes the state of the player after one tick.
    /// </summary>
    /// <param name="position">The current position of the player's feet.</param>
    /// <param name="velocity">The current velocity in blocks per tick.</param>
    /// <param name="onGround">Whether the player was standing on the ground.</param>
    /// <param name="input">What the player is trying to do.</param>
    public PhysicsStep Step(Vector3d position, Vector3d velocity, bool onGround, MovementInput input)
    {
        var horizontal = Steer(velocity, onGround, input);

        var jumping = input.Jump && onGround;

        var verticalVelocity = jumping
            // A jump replaces this tick's fall instead of being damped by it.
            ? JumpVelocity
            : Math.Max((velocity.Y - Gravity) * VerticalDrag, -TerminalVelocity);

        var y = ResolveVertical(position, verticalVelocity, out var landed);

        if (landed)
            verticalVelocity = 0;

        var afterVertical = position with { Y = y };

        // Stepping up is only possible from the ground, otherwise the player
        // would climb walls mid-jump. Not on the tick the player jumped either:
        // the jump is already the way up, and stepping as well would add the
        // obstacle's height on top of it, putting the player on the obstacle
        // with a full jump still left to spend -- which throws it off the far
        // side instead of landing it on top.
        var mayStepUp = input.AllowStepUp && !jumping && (onGround || landed);
        var protectFromLedges = input.Mode == MovementMode.Sneak && (onGround || landed);

        var afterX = ResolveHorizontal(afterVertical, Damp(horizontal.X), Axis.X, mayStepUp, protectFromLedges, out var blockedX);
        var afterZ = ResolveHorizontal(afterX, Damp(horizontal.Z), Axis.Z, mayStepUp, protectFromLedges, out var blockedZ);

        var newVelocity = new Vector3d(
            blockedX ? 0 : Damp(horizontal.X),
            verticalVelocity,
            blockedZ ? 0 : Damp(horizontal.Z));

        return new PhysicsStep(afterZ, newVelocity, landed || IsSupported(afterZ), blockedX || blockedZ);
    }

    /// <summary>
    /// The horizontal velocity the player is trying for this tick.
    /// </summary>
    /// <remarks>
    /// On the ground the input sets the speed directly; the ramp-up of the real
    /// game is not modelled. In the air it can only nudge: the player keeps what
    /// momentum it has, minus drag, plus <see cref="AirControl"/>. That is the
    /// difference between hopping onto a block and sailing over it.
    /// </remarks>
    private static Vector3d Steer(Vector3d velocity, bool onGround, MovementInput input)
    {
        if (onGround)
            return input.Direction is null
                ? new Vector3d(velocity.X * HorizontalDrag * GroundFriction, 0, velocity.Z * HorizontalDrag * GroundFriction)
                : Normalize(input.Direction) * SpeedOf(input.Mode);

        var drifting = new Vector3d(velocity.X * HorizontalDrag, 0, velocity.Z * HorizontalDrag);

        if (input.Direction is null)
            return drifting;

        var steering = Normalize(input.Direction) * AirControl;

        return new Vector3d(drifting.X + steering.X, 0, drifting.Z + steering.Z);
    }

    private static Vector3d Normalize(Vector3d direction)
    {
        var length = Math.Sqrt(direction.X * direction.X + direction.Z * direction.Z);

        return length < Epsilon
            ? Vector3d.Zero
            : new Vector3d(direction.X / length, 0, direction.Z / length);
    }

    private static double Damp(double velocity)
        => Math.Abs(velocity) < NegligibleVelocity ? 0 : velocity;

    /// <summary>
    /// Moves along the Y axis, stopping at the surface that was hit.
    /// </summary>
    private double ResolveVertical(Vector3d position, double delta, out bool landed)
    {
        landed = false;

        var target = position.Y + delta;

        if (!CollidesAt(position with { Y = target }))
            return target;

        if (delta <= 0)
        {
            var landing = HighestSurfaceBetween(position, target, position.Y);

            // No surface below means the player is already inside geometry, either
            // because the chunk has not arrived or because it spawned in a block.
            // Staying put is safer than sinking through the world.
            landed = true;

            return landing ?? position.Y;
        }

        // Rising: stop just below the ceiling that was hit.
        return Math.Floor(target + Height) - Height;
    }

    /// <summary>
    /// Finds the top of the highest solid block the player's footprint would pass
    /// through while falling from <paramref name="from"/> to <paramref name="to"/>.
    /// </summary>
    private double? HighestSurfaceBetween(Vector3d position, double to, double from)
    {
        double? highest = null;

        var lowestBlock = (int)Math.Floor(to);
        var highestBlock = (int)Math.Floor(from);

        foreach (var (blockX, blockZ) in Footprint(position))
        {
            for (var blockY = highestBlock; blockY >= lowestBlock; blockY--)
            {
                if (!IsSolid(blockX, blockY, blockZ))
                    continue;

                var top = blockY + 1.0;

                if (top <= from && (highest is null || top > highest))
                    highest = top;

                break;
            }
        }

        return highest;
    }

    /// <summary>
    /// Moves along a horizontal axis, stepping onto low obstacles when allowed and
    /// refusing to leave solid ground while sneaking.
    /// </summary>
    private Vector3d ResolveHorizontal(Vector3d position, double delta, Axis axis, bool mayStepUp, bool protectFromLedges, out bool blocked)
    {
        blocked = false;

        if (delta == 0)
            return position;

        var moved = Offset(position, axis, delta);

        if (!CollidesAt(moved))
        {
            // Sneaking refuses any move that would leave the player standing on nothing.
            if (protectFromLedges && !IsSupported(moved))
                return position;

            return moved;
        }

        if (mayStepUp)
        {
            var stepped = TryStepUp(position, moved);

            if (stepped is not null)
                return stepped;
        }

        blocked = true;

        return position;
    }

    /// <summary>
    /// Attempts to climb onto an obstacle by raising the move by at most
    /// <see cref="StepHeight"/>.
    /// </summary>
    /// <returns>The raised position, or <c>null</c> if it does not fit.</returns>
    private Vector3d? TryStepUp(Vector3d origin, Vector3d blockedTarget)
    {
        // The obstacle's top has to be within reach, so only surfaces inside the
        // step height are candidates.
        var surface = LowestSurfaceAbove(blockedTarget, origin.Y);

        if (surface is null || surface.Value - origin.Y > StepHeight)
            return null;

        var raised = blockedTarget with { Y = surface.Value };

        // The full body has to fit at the new height, and the step has to end on
        // something rather than in mid-air.
        if (CollidesAt(raised) || !IsSupported(raised))
            return null;

        return raised;
    }

    /// <summary>
    /// The top of the lowest solid block within the footprint at or above <paramref name="from"/>.
    /// </summary>
    private double? LowestSurfaceAbove(Vector3d position, double from)
    {
        double? lowest = null;

        var startBlock = (int)Math.Floor(from);
        var endBlock = (int)Math.Floor(from + StepHeight);

        foreach (var (blockX, blockZ) in Footprint(position))
        {
            for (var blockY = startBlock; blockY <= endBlock; blockY++)
            {
                if (!IsSolid(blockX, blockY, blockZ))
                    continue;

                var top = blockY + 1.0;

                if (lowest is null || top > lowest)
                    lowest = top;

                break;
            }
        }

        return lowest;
    }

    /// <summary>
    /// Determines whether there is solid ground directly beneath the player.
    /// </summary>
    private bool IsSupported(Vector3d position)
        => CollidesAt(position with { Y = position.Y - Epsilon });

    /// <summary>
    /// Determines whether the player's bounding box overlaps a solid block.
    /// </summary>
    private bool CollidesAt(Vector3d position)
    {
        var bottom = (int)Math.Floor(position.Y);
        var top = (int)Math.Floor(position.Y + Height - Epsilon);

        foreach (var (blockX, blockZ) in Footprint(position))
            for (var blockY = bottom; blockY <= top; blockY++)
                if (IsSolid(blockX, blockY, blockZ))
                    return true;

        return false;
    }

    /// <summary>
    /// The block columns the player's bounding box covers.
    /// </summary>
    private static IEnumerable<(int X, int Z)> Footprint(Vector3d position)
    {
        var minX = (int)Math.Floor(position.X - HalfWidth);
        var maxX = (int)Math.Floor(position.X + HalfWidth - Epsilon);
        var minZ = (int)Math.Floor(position.Z - HalfWidth);
        var maxZ = (int)Math.Floor(position.Z + HalfWidth - Epsilon);

        for (var x = minX; x <= maxX; x++)
            for (var z = minZ; z <= maxZ; z++)
                yield return (x, z);
    }

    private static Vector3d Offset(Vector3d position, Axis axis, double delta)
        => axis == Axis.X
            ? position with { X = position.X + delta }
            : position with { Z = position.Z + delta };

    private bool IsSolid(int x, int y, int z)
        => BlockCollision.IsSolid(world.GetBlock(new Vector3i(x, y, z)));

    private enum Axis
    {
        X,
        Z
    }
}

/// <summary>
/// The result of advancing the player by one tick.
/// </summary>
/// <param name="Position">The new position.</param>
/// <param name="Velocity">The new velocity.</param>
/// <param name="OnGround">Whether the player is standing on solid ground.</param>
/// <param name="Blocked">Whether horizontal movement was stopped by geometry.</param>
internal record PhysicsStep(Vector3d Position, Vector3d Velocity, bool OnGround, bool Blocked);
