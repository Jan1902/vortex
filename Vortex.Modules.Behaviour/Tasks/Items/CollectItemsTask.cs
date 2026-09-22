using Microsoft.Extensions.Logging;
using Vortex.Data;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Tasks.Navigation;
using Vortex.Modules.Entities.Abstraction;
using Vortex.Modules.Inventory.Abstraction;
using Vortex.Modules.Navigation.Abstraction;
using Vortex.Modules.Player.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Tasks.Items;

/// <summary>
/// Picks up the items lying around a place.
/// </summary>
/// <remarks>
/// <para>
/// Picking up is the server's doing: it hands an item to a player who comes
/// close enough and has room for it. So this task only walks up to the items,
/// one after the other, nearest first, and is done when none is left that the
/// inventory could take.
/// </para>
/// <para>
/// An item cannot be picked up for a moment after it was dropped. Standing on
/// it, the task waits that out rather than walking on.
/// </para>
/// </remarks>
public class CollectItemsTask(
    Vector3d center,
    double radius,
    IEntityManager entities,
    IInventoryManager inventory,
    IPlayerManager player,
    Func<Vector3i, MovementCapabilities, GoToTask> goTo,
    ILogger<CollectItemsTask> logger) : BotTask
{
    /// <summary>How long to wait for an item that is not being picked up although the bot stands on it.</summary>
    private static readonly TimeSpan PickupWait = TimeSpan.FromSeconds(3);

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    public override string Description
        => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"collect the items within {radius:0.#} blocks of {center.X:F0} {center.Y:F0} {center.Z:F0}");

    public override bool IsSatisfied()
        => NextItem() is null;

    public override IEnumerable<BotTask> Dependencies()
    {
        if (NextItem() is { } item)
            yield return goTo(item.Position.ToBlockPosition(), MovementCapabilities.Athletic);
    }

    public override async Task<TaskResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        if (NextItem() is not { } item)
            return TaskResult.Success();

        logger.LogDebug("Waiting to pick up {Item} at {Position}", item.Item, item.Position);

        var deadline = DateTime.UtcNow + PickupWait;

        while (entities.Get(item.Id) is not null)
        {
            if (DateTime.UtcNow > deadline)
                return TaskResult.Failed($"{item.Item?.Item} at {item.Position.ToBlockPosition()} is not being picked up");

            await Task.Delay(PollInterval, cancellationToken);
        }

        return TaskResult.Success();
    }

    /// <summary>
    /// The nearest item in the area that the inventory has room for. Items whose
    /// stack the server has not described yet are left for the next round.
    /// </summary>
    private Entity? NextItem()
        => entities.Entities
            .Where(entity => entity.Type == EntityType.Item
                && entity.Item is { } stack
                && entity.Position.DistanceTo(center) <= radius
                && inventory.SpaceFor(stack.Item) > 0)
            .MinBy(entity => entity.Position.DistanceTo(player.Position));
}
