using Vortex.Shared;

namespace Vortex.Modules.Navigation.Abstraction;

/// <summary>
/// A route through the world, as the moves to make in turn.
/// </summary>
/// <remarks>
/// Called Route rather than Path so that it does not collide with System.IO.Path
/// in every file that uses it.
/// </remarks>
/// <param name="Moves">
/// The moves to make, in order, starting from the position the route was
/// searched from. Empty when the search started where it was meant to end.
/// </param>
/// <param name="ReachesGoal">
/// Whether this route ends where it was asked to. False for a route that only
/// goes as far as the client can currently see: the world arrives a chunk at a
/// time, and somewhere a hundred blocks off is not unreachable, just not known
/// yet. Walking what is known and asking again is how the bot gets there.
/// </param>
/// <param name="Origin">
/// The block the route was planned from, which is not always the one the
/// player's middle is over: at the lip of a drop its feet are on the ledge
/// behind. Null for a route built by hand rather than searched.
/// </param>
public record Route(IReadOnlyList<Move> Moves, bool ReachesGoal = true, Vector3i? Origin = null)
{
    /// <summary>Gets the move to make next, or null if there is none.</summary>
    public Move? Next
        => Moves.Count > 0 ? Moves[0] : null;

    /// <summary>
    /// Gets the blocks the route passes over, in order.
    /// </summary>
    /// <remarks>
    /// A read-only view for whatever only cares where the route goes and not how
    /// it gets there -- drawing it, logging it, measuring it. Anything that has
    /// to carry the route out wants <see cref="Moves"/>, because the how is the
    /// part that cannot be guessed back from the positions.
    /// </remarks>
    public IEnumerable<Vector3i> Positions
        => Moves.Select(move => move.To);

    /// <summary>Gets the block the route ends on, or null if it is empty.</summary>
    public Vector3i? Destination
        => Moves.Count > 0 ? Moves[^1].To : null;

    /// <summary>
    /// The furthest block reachable from <paramref name="from"/> in one straight
    /// walk, so that a run of blocks in a line can be covered in a single
    /// movement instead of one at a time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only plain walking runs together, and only while the direction holds.
    /// Everything else -- a step up to jump against, a drop to take on its own,
    /// anything that has to be mined or bridged first -- is its own movement and
    /// ends the run, because each needs to be aimed at afresh.
    /// </para>
    /// <para>
    /// This is the route's own idea of a straight line, from the block grid
    /// alone. Where the player actually stands within that block is the caller's
    /// problem.
    /// </para>
    /// </remarks>
    /// <param name="from">The block the walk starts from.</param>
    /// <returns>
    /// The block to aim for, or null when the route does not start with a walk.
    /// </returns>
    public Vector3i? FurthestWalk(Vector3i from)
    {
        if (Moves.Count == 0 || Moves[0] is not Walk first)
            return null;

        var direction = first.To - from;
        var furthest = 0;

        while (furthest + 1 < Moves.Count
            && Moves[furthest + 1] is Walk next
            && next.To - Moves[furthest].To == direction)
        {
            furthest++;
        }

        return Moves[furthest].To;
    }
}
