using Vortex.Shared;

namespace Vortex.Modules.Navigation.Abstraction;

/// <summary>
/// Works out how to get from one place to another. It plans, it does not move.
/// </summary>
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
    /// budget before it found anything.
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
    Route? FindRoute(Vector3i start, Vector3i goal, MovementCapabilities? capabilities = null);

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
    Route? FindRoute(Vector3d start, Vector3i goal, MovementCapabilities? capabilities = null);
}
