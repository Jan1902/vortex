namespace Vortex.Shared;

/// <summary>
/// Whether the player can see a block from where its eyes are, and where on it.
/// </summary>
/// <remarks>
/// <para>
/// Being close enough is not the same as being able to get at a block. A player
/// breaks, uses and places against what it is looking at, and it cannot look
/// through a wall of dirt at the coal behind it. Doing so anyway works on a
/// server that only checks the distance, and is exactly what gives a bot away.
/// </para>
/// <para>
/// Shared because two sides have to agree on it: the pathfinder, choosing
/// where to stand, and whatever then turns to the block and acts on it.
/// </para>
/// <para>
/// A side of the block counts as seen when a straight line from the eyes to a
/// point on it passes through nothing that blocks the view. The middle of the
/// side is tried first, then points towards its corners, so that a block
/// peeking round an edge still counts. A side with a block against it is not
/// tried at all: there is nothing of it to see.
/// </para>
/// </remarks>
public static class LineOfSight
{
    /// <summary>How high above its feet the player's eyes are.</summary>
    public const double EyeHeight = 1.62;

    /// <summary>How far from the middle of a side, towards its corners, the other points tried are.</summary>
    private const double Inset = 0.35;

    /// <summary>How far into the block a point on its surface is moved, so the line ends inside it.</summary>
    private const double Into = 0.001;

    /// <summary>The six sides of a block, as the direction each one faces.</summary>
    private static readonly Vector3i[] _sides =
    [
        new(0, 1, 0),
        new(0, -1, 0),
        new(1, 0, 0),
        new(-1, 0, 0),
        new(0, 0, 1),
        new(0, 0, -1),
    ];

    /// <summary>Where the eyes of a player standing at a position are.</summary>
    public static Vector3d Eyes(Vector3d feet)
        => feet + new Vector3d(0, EyeHeight, 0);

    /// <summary>
    /// A point on a block the eyes can see, and the side it is on, or null if
    /// no part of it can be seen.
    /// </summary>
    /// <param name="eyes">Where the eyes are.</param>
    /// <param name="target">The block to look at.</param>
    /// <param name="blocksView">Whether a block is in the way of anything seen through it.</param>
    /// <returns>
    /// The point, and the direction the side it is on faces. For a target that
    /// does not block the view itself, such as the air a block is to be placed
    /// in, its middle, if that can be seen, with the side facing up.
    /// </returns>
    public static (Vector3d Point, Vector3i Side)? Sight(Vector3d eyes, Vector3i target, Func<Vector3i, bool> blocksView)
    {
        var middle = new Vector3d(target.X + 0.5, target.Y + 0.5, target.Z + 0.5);

        if (!blocksView(target))
            return IsClear(eyes, middle, target, blocksView) ? (middle, new Vector3i(0, 1, 0)) : null;

        foreach (var side in _sides)
        {
            var centre = new Vector3d(middle.X + side.X * 0.5, middle.Y + side.Y * 0.5, middle.Z + side.Z * 0.5);

            // Only the sides turned towards the eyes, and only those not
            // covered by a neighbour.
            var toEyes = eyes - centre;

            if (toEyes.X * side.X + toEyes.Y * side.Y + toEyes.Z * side.Z <= 0)
                continue;

            if (blocksView(target + side))
                continue;

            foreach (var point in PointsOn(centre, side))
            {
                var inside = new Vector3d(point.X - side.X * Into, point.Y - side.Y * Into, point.Z - side.Z * Into);

                if (IsClear(eyes, inside, target, blocksView))
                    return (point, side);
            }
        }

        return null;
    }

    /// <summary>The middle of a side, and then points towards each of its corners.</summary>
    private static IEnumerable<Vector3d> PointsOn(Vector3d centre, Vector3i side)
    {
        yield return centre;

        // Two directions along the side, whichever two axes it does not face.
        var (u, v) = side.Y != 0
            ? (new Vector3d(1, 0, 0), new Vector3d(0, 0, 1))
            : side.X != 0
                ? (new Vector3d(0, 1, 0), new Vector3d(0, 0, 1))
                : (new Vector3d(1, 0, 0), new Vector3d(0, 1, 0));

        foreach (var (a, b) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
            yield return centre + u * (a * Inset) + v * (b * Inset);
    }

    /// <summary>
    /// Whether the straight line from the eyes to a point inside the target
    /// reaches the target before anything blocks it.
    /// </summary>
    /// <remarks>
    /// Walks the blocks the line passes through in order, the way a ray is
    /// traced through a grid. The block the eyes are in is never in the way:
    /// the player's own head is there.
    /// </remarks>
    private static bool IsClear(Vector3d from, Vector3d to, Vector3i target, Func<Vector3i, bool> blocksView)
    {
        var cell = from.ToBlockPosition();
        var direction = to - from;

        var (stepX, tMaxX, tDeltaX) = Axis(from.X, direction.X);
        var (stepY, tMaxY, tDeltaY) = Axis(from.Y, direction.Y);
        var (stepZ, tMaxZ, tDeltaZ) = Axis(from.Z, direction.Z);

        var (x, y, z) = (cell.X, cell.Y, cell.Z);

        // Every block along the way is one step, and the line is short.
        for (var steps = 0; steps < 64; steps++)
        {
            var at = new Vector3i(x, y, z);

            if (at == target)
                return true;

            if (steps > 0 && blocksView(at))
                return false;

            if (tMaxX > 1 && tMaxY > 1 && tMaxZ > 1)
                return false;

            if (tMaxX <= tMaxY && tMaxX <= tMaxZ)
            {
                x += stepX;
                tMaxX += tDeltaX;
            }
            else if (tMaxY <= tMaxZ)
            {
                y += stepY;
                tMaxY += tDeltaY;
            }
            else
            {
                z += stepZ;
                tMaxZ += tDeltaZ;
            }
        }

        return false;
    }

    /// <summary>
    /// Which way the line steps along one axis, how far along it the first
    /// block boundary is, and how far apart the boundaries are after that --
    /// the last two as fractions of the whole line.
    /// </summary>
    private static (int Step, double First, double Every) Axis(double start, double length)
    {
        if (Math.Abs(length) < 1e-12)
            return (0, double.PositiveInfinity, double.PositiveInfinity);

        var step = Math.Sign(length);
        var boundary = step > 0 ? Math.Floor(start) + 1 : Math.Floor(start);

        return (step, (boundary - start) / length, Math.Abs(1 / length));
    }
}
