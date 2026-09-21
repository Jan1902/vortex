namespace Vortex.Shared;

/// <summary>
/// The size of the box a player occupies.
/// </summary>
/// <remarks>
/// Shared because the player is wider than the block its middle sits in, and
/// more than one thing has to know that. The physics needs it to work out what
/// the player collides with and what holds it up; the pathfinder needs it to
/// work out which block the player is actually standing on, which at the lip of
/// a drop is not the one its centre is over.
/// </remarks>
public static class PlayerHitbox
{
    /// <summary>Width of the box, centred on the player's position.</summary>
    public const double Width = 0.6;

    /// <summary>Height of the box, measured up from the player's feet.</summary>
    public const double Height = 1.8;

    /// <summary>How far the box reaches to either side of the player's middle.</summary>
    public const double HalfWidth = Width / 2;

    /// <summary>
    /// The block columns the player's box covers.
    /// </summary>
    /// <remarks>
    /// One column when it stands clear of any edge, up to four when it straddles
    /// them.
    /// </remarks>
    public static IEnumerable<Vector2i> ColumnsUnder(Vector3d position)
    {
        // A hair inside the far edge, so a player resting exactly on a boundary
        // is not counted as being in the block it is only touching.
        const double epsilon = 0.0001;

        var minX = (int)Math.Floor(position.X - HalfWidth);
        var maxX = (int)Math.Floor(position.X + HalfWidth - epsilon);
        var minZ = (int)Math.Floor(position.Z - HalfWidth);
        var maxZ = (int)Math.Floor(position.Z + HalfWidth - epsilon);

        for (var x = minX; x <= maxX; x++)
            for (var z = minZ; z <= maxZ; z++)
                yield return new Vector2i(x, z);
    }
}
