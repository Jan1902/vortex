using Vortex.Modules.World.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Player;

/// <summary>
/// Advances the player's position by one tick, applying gravity and collision.
/// </summary>
internal class PlayerPhysics(IWorldManager world)
{
    /// <summary>Blocks per tick squared that the player is pulled down by.</summary>
    public const double Gravity = 0.08;

    /// <summary>Factor applied to vertical velocity each tick.</summary>
    public const double VerticalDrag = 0.98;

    /// <summary>Factor applied to horizontal velocity each tick while on ground.</summary>
    public const double HorizontalDrag = 0.91;

    /// <summary>Speed at which falling stops accelerating.</summary>
    public const double TerminalVelocity = 3.92;

    /// <summary>Width of the player's bounding box, centred on its position.</summary>
    public const double Width = 0.6;

    /// <summary>Height of the player's bounding box, measured up from its feet.</summary>
    public const double Height = 1.8;

    /// <summary>Velocities below this are treated as standing still.</summary>
    private const double NegligibleVelocity = 0.003;

    private const double HalfWidth = Width / 2;

    /// <summary>
    /// Computes the state of the player after one tick.
    /// </summary>
    /// <param name="position">The current position of the player's feet.</param>
    /// <param name="velocity">The current velocity in blocks per tick.</param>
    /// <returns>The position, velocity and ground contact after the tick.</returns>
    public PhysicsStep Step(Vector3d position, Vector3d velocity)
    {
        var verticalVelocity = Math.Max((velocity.Y - Gravity) * VerticalDrag, -TerminalVelocity);

        // Axes are resolved one at a time so that sliding along a wall keeps the
        // movement along the other axes intact.
        var y = ResolveVertical(position, verticalVelocity, out var onGround);

        if (onGround)
            verticalVelocity = 0;

        var afterVertical = position with { Y = y };

        var x = ResolveHorizontal(afterVertical, velocity.X, Axis.X);
        var afterX = afterVertical with { X = x };

        var z = ResolveHorizontal(afterX, velocity.Z, Axis.Z);

        var horizontalDrag = onGround ? HorizontalDrag : 1.0;

        var newVelocity = new Vector3d(
            Damp(x == afterVertical.X + velocity.X ? velocity.X * horizontalDrag : 0),
            verticalVelocity,
            Damp(z == afterX.Z + velocity.Z ? velocity.Z * horizontalDrag : 0));

        return new PhysicsStep(new Vector3d(x, y, z), newVelocity, onGround);
    }

    private static double Damp(double velocity)
        => Math.Abs(velocity) < NegligibleVelocity ? 0 : velocity;

    /// <summary>
    /// Moves along the Y axis, stopping at the surface that was hit.
    /// </summary>
    private double ResolveVertical(Vector3d position, double delta, out bool onGround)
    {
        onGround = false;

        var target = position.Y + delta;

        if (!CollidesAt(position with { Y = target }))
        {
            // Standing still on a surface still counts as being on the ground.
            onGround = delta <= 0 && CollidesAt(position with { Y = position.Y - 0.0001 });

            return target;
        }

        if (delta <= 0)
        {
            // Falling: land on the highest block top between here and the target.
            var landing = HighestSurfaceBetween(position, target, position.Y);

            // No surface below means the player is already inside geometry, either
            // because the chunk has not arrived or because it spawned in a block.
            // Staying put is safer than sinking through the world.
            onGround = true;

            return landing ?? position.Y;
        }

        // Rising: stop just below the ceiling that was hit.
        var ceiling = Math.Floor(target + Height);

        return ceiling - Height;
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

                // Only surfaces the player actually descends onto count.
                if (top <= from && (highest is null || top > highest))
                    highest = top;

                break;
            }
        }

        return highest;
    }

    /// <summary>
    /// Moves along a horizontal axis, refusing the move if it would end inside a block.
    /// </summary>
    private double ResolveHorizontal(Vector3d position, double delta, Axis axis)
    {
        var current = axis == Axis.X ? position.X : position.Z;

        if (delta == 0)
            return current;

        var target = current + delta;

        var moved = axis == Axis.X
            ? position with { X = target }
            : position with { Z = target };

        // Anything finer than "blocked or not" needs real collision shapes.
        return CollidesAt(moved) ? current : target;
    }

    /// <summary>
    /// Determines whether the player's bounding box overlaps a solid block.
    /// </summary>
    private bool CollidesAt(Vector3d position)
    {
        var bottom = (int)Math.Floor(position.Y);
        var top = (int)Math.Floor(position.Y + Height - 0.0001);

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
        var maxX = (int)Math.Floor(position.X + HalfWidth - 0.0001);
        var minZ = (int)Math.Floor(position.Z - HalfWidth);
        var maxZ = (int)Math.Floor(position.Z + HalfWidth - 0.0001);

        for (var x = minX; x <= maxX; x++)
            for (var z = minZ; z <= maxZ; z++)
                yield return (x, z);
    }

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
internal record PhysicsStep(Vector3d Position, Vector3d Velocity, bool OnGround);
