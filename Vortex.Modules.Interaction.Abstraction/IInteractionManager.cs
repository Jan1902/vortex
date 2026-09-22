using Vortex.Shared;

namespace Vortex.Modules.Interaction.Abstraction;

/// <summary>
/// Acts on the world with what is in hand: clicking blocks, using items.
/// </summary>
/// <remarks>
/// Only the acts themselves. What is in hand, and which way the bot looks, are
/// the inventory's and the player's business; callers set those up first.
/// Every act that changes blocks is numbered, and can be awaited until the
/// server confirms it has dealt with it -- the block changes themselves still
/// arrive the usual way.
/// </remarks>
public interface IInteractionManager
{
    /// <summary>
    /// Uses the item in hand on a side of a block: opens a door or a chest,
    /// flips a lever, or places a block against it.
    /// </summary>
    /// <param name="cursor">Where on the side it is clicked, from 0 to 1 on each axis; the middle if not given.</param>
    /// <returns>Whether the server confirmed it before <paramref name="cancellationToken"/> or a timeout ran out.</returns>
    Task<bool> UseItemOnBlockAsync(Vector3i block, BlockFace face, Hand hand = Hand.Main, Vector3f? cursor = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uses the item in hand on its own, as eating or throwing does.
    /// </summary>
    /// <param name="yaw">Which way the bot looks while using it, which decides where thrown things fly.</param>
    /// <param name="pitch">How far up or down it looks.</param>
    /// <returns>Whether the server confirmed it.</returns>
    Task<bool> UseItemAsync(float yaw, float pitch, Hand hand = Hand.Main, CancellationToken cancellationToken = default);

    /// <summary>
    /// Breaks a block: starts, keeps at it for as long as the caller says it
    /// takes, and finishes.
    /// </summary>
    /// <param name="ticks">
    /// How long breaking takes with what is in hand, in ticks; 0 for a block
    /// that breaks at once. The server checks it, so finishing early fails.
    /// </param>
    /// <returns>Whether the server confirmed finishing. The block itself changes the usual way.</returns>
    Task<bool> DigAsync(Vector3i block, BlockFace face, int ticks, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hits an entity with what is in the main hand. The server does not answer;
    /// the damage shows in the entity's health, or its death in its removal.
    /// </summary>
    Task AttackAsync(int entityId, bool sneaking = false);

    /// <summary>
    /// Uses an entity with what is in hand, as right-clicking it does: trading
    /// with a villager, milking a cow, getting into a boat.
    /// </summary>
    /// <param name="point">Where on the entity, relative to its position, for entities that care, such as armour stands.</param>
    Task InteractWithEntityAsync(int entityId, Hand hand = Hand.Main, Vector3f? point = null, bool sneaking = false);

    /// <summary>Swings an arm, which others see and some things react to.</summary>
    Task SwingAsync(Hand hand = Hand.Main);
}
