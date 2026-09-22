using Vortex.Shared;

namespace Vortex.Modules.Entities.Abstraction;

/// <summary>
/// Keeps track of the entities around the bot, as the server reports them.
/// </summary>
/// <remarks>
/// The server only tells the client about entities within its tracking range,
/// and says when one leaves it, so this is what the bot can see rather than
/// everything in the world.
/// </remarks>
public interface IEntityManager
{
    /// <summary>
    /// The ID of the bot's own entity, or <c>null</c> before joining. The bot
    /// itself is never among <see cref="Entities"/>.
    /// </summary>
    int? SelfId { get; }

    /// <summary>
    /// Every entity currently tracked.
    /// </summary>
    IReadOnlyCollection<Entity> Entities { get; }

    /// <summary>Gets an entity by the ID packets refer to it by.</summary>
    Entity? Get(int id);

    /// <summary>Gets an entity by its UUID; for a player, the account's UUID.</summary>
    Entity? Get(Guid uuid);

    /// <summary>
    /// Finds the entity closest to a position.
    /// </summary>
    /// <param name="filter">Restricts which entities count, such as only zombies.</param>
    /// <returns>The closest entity, or <c>null</c> if none qualifies.</returns>
    Entity? Nearest(Vector3d from, Func<Entity, bool>? filter = null);
}
