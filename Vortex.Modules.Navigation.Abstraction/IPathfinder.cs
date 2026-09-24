using Vortex.Shared;

namespace Vortex.Modules.Navigation.Abstraction;

/// <summary>
/// Works out how to get from one place to another. It plans, it does not move.
/// </summary>
/// <remarks>
/// <para>
/// A search has a time budget rather than going on until it is sure. Where the
/// goal is further than it can get to in that time, it hands back the part of
/// the way it has worked out, marked <see cref="Route.Truncated"/>, and the rest
/// is searched from the end of that. This is what lets the bot set off on a long
/// way without first waiting to have planned all of it.
/// </para>
/// <para>
/// Every search can be handed the route it is replacing. The new one then keeps
/// to the old one wherever that is about as good, rather than switching between
/// two ways of the same price every time it is asked again.
/// </para>
/// </remarks>
public interface IPathfinder
{
    /// <summary>
    /// Searches for a walkable route.
    /// </summary>
    /// <param name="start">The block position to start from, at foot level.</param>
    /// <param name="goal">The block position to reach, at foot level.</param>
    /// <returns>
    /// The route, or null if there is none -- because the goal cannot be stood
    /// on, because nothing connects the two, or because the search ran out of
    /// time before it found any way that gets the player anywhere.
    /// </returns>
    /// <remarks>
    /// The answer is only as good as the world currently loaded. Chunks that have
    /// not arrived are treated as solid, so a path will not be routed through a
    /// part of the world the client cannot see yet.
    /// </remarks>
    /// <param name="capabilities">
    /// What the player may do on the way. Only moves allowed here are ever
    /// planned, so this is what decides whether a gap becomes a jump or a
    /// detour.
    /// </param>
    /// <param name="previous">The route this one replaces, if any, to keep close to.</param>
    Route? FindRoute(Vector3i start, Vector3i goal, MovementCapabilities? capabilities = null, Route? previous = null);

    /// <summary>
    /// Searches for a walkable route from where the player actually is.
    /// </summary>
    /// <remarks>
    /// The player is wider than the block its middle sits in. At the lip of a
    /// drop its feet are still on the ledge while its centre is already over the
    /// edge, and the block it is standing on is the one behind it, not the one
    /// below. Handing over the exact position lets that be worked out rather
    /// than guessed.
    /// </remarks>
    /// <param name="start">Where the player's feet are.</param>
    /// <param name="goal">The block position to reach, at foot level.</param>
    /// <param name="capabilities">What the player may do on the way.</param>
    /// <param name="previous">The route this one replaces, if any, to keep close to.</param>
    Route? FindRoute(Vector3d start, Vector3i goal, MovementCapabilities? capabilities = null, Route? previous = null);

    /// <summary>
    /// Plans a route to the nearest place a block is within reach from.
    /// </summary>
    /// <remarks>
    /// For touching something rather than standing somewhere: breaking a block,
    /// opening a chest. The place is one the player can stand, from whose middle
    /// the block's centre is no further than the reach from the eyes, from which
    /// some part of the block can be seen, and that is neither the block itself
    /// nor under it. Reaching through a wall to a block behind it is not on.
    /// </remarks>
    /// <param name="start">Where the player is.</param>
    /// <param name="target">The block to get within reach of.</param>
    /// <param name="reach">How far from the eyes the block's centre may be.</param>
    /// <param name="capabilities">What the route may ask of the player.</param>
    /// <param name="previous">The route this one replaces, if any, to keep close to.</param>
    /// <returns>The route, or <c>null</c> if there is none.</returns>
    Route? FindRouteWithinReach(Vector3d start, Vector3i target, double reach, MovementCapabilities? capabilities = null, Route? previous = null);

    /// <summary>
    /// Plans a route to the nearest place close by a block, the block itself
    /// included.
    /// </summary>
    /// <remarks>
    /// For being next to something rather than on it: an item lying in a gap
    /// too low to stand in is picked up from the block beside it.
    /// </remarks>
    /// <param name="start">Where the player is.</param>
    /// <param name="target">The block to get close to.</param>
    /// <param name="range">How many blocks off it, sideways, the place may be; it may also be one higher or lower.</param>
    /// <param name="capabilities">What the route may ask of the player.</param>
    /// <param name="previous">The route this one replaces, if any, to keep close to.</param>
    /// <returns>The route, or <c>null</c> if there is none.</returns>
    Route? FindRouteNear(Vector3d start, Vector3i target, int range, MovementCapabilities? capabilities = null, Route? previous = null);

    /// <summary>
    /// Whether a move planned earlier can still be made, in the world as it is
    /// now.
    /// </summary>
    /// <remarks>
    /// Answered by the same code that planned the move, so a move passes exactly
    /// when a search from the same block would still offer it. Cheap enough to
    /// ask before every move: it looks at the blocks around one position, not at
    /// the whole route.
    /// </remarks>
    /// <param name="from">The block the move starts from.</param>
    /// <param name="move">The move.</param>
    /// <param name="capabilities">What the player may do, as for the search that planned it.</param>
    bool CanStillMake(Vector3i from, Move move, MovementCapabilities? capabilities = null);
}
