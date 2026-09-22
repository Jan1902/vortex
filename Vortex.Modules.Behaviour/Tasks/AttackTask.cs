using Microsoft.Extensions.Logging;
using Vortex.Data;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Entities.Abstraction;
using Vortex.Modules.Interaction.Abstraction;
using Vortex.Modules.Inventory.Abstraction;
using Vortex.Modules.Navigation.Abstraction;
using Vortex.Modules.Player.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Tasks;

/// <summary>
/// Fights an entity until it is gone.
/// </summary>
/// <remarks>
/// <para>
/// Done once the entity has no health left or is no longer tracked. Each round
/// either closes in on it or lands one hit with the best weapon carried, then
/// waits out the weapon's cooldown, since a hit before that does less damage.
/// </para>
/// <para>
/// The entity counts as small, going by <see cref="Entity.AssumedSize"/>, so the
/// bot comes close and aims for its middle.
/// </para>
/// </remarks>
public class AttackTask(
    int entityId,
    IEntityManager entities,
    IInventoryManager inventory,
    IPlayerManager player,
    IInteractionManager interaction,
    Func<Vector3i, MovementCapabilities, GoToTask> goTo,
    ILogger<AttackTask> logger) : BotTask
{
    /// <summary>How close the entity has to be to hit it, from the eyes to its middle.</summary>
    private const double Reach = 3.0;

    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(50);

    public override string Description
        => entities.Get(entityId) is { } entity ? $"fight the {entity.Type} {entityId}" : $"fight entity {entityId}";

    // Dead as soon as its health reaches nothing: the entity itself lingers for
    // its death animation, and hitting that is wasted.
    public override bool IsSatisfied()
        => entities.Get(entityId) is null or { Health: <= 0 };

    public override IEnumerable<BotTask> Dependencies()
    {
        if (entities.Get(entityId) is { } entity && !InReach(entity))
            yield return goTo(entity.Position.ToBlockPosition(), MovementCapabilities.Athletic);
    }

    public override async Task<TaskResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        if (entities.Get(entityId) is not { } entity)
            return TaskResult.Success();

        var before = inventory.HeldItem?.Item;

        if (BestWeapon() is { } weapon)
            await Hold.InMainHandAsync(inventory, weapon);

        var held = inventory.HeldItem?.Item;

        // Changing what is held starts the cooldown over; hitting before it has
        // passed wastes most of the hit.
        if (held != before)
            await Task.Delay(Cooldown(held), cancellationToken);

        player.LookAt(entity.Center);
        await Task.Delay(Tick, cancellationToken);

        logger.LogDebug("Hitting {Type} {EntityId} with {Weapon}", entity.Type, entityId, held?.ToString() ?? "the bare hand");

        await interaction.AttackAsync(entityId);

        // A full-strength hit needs the weapon's cooldown to have passed.
        await Task.Delay(Cooldown(held), cancellationToken);

        return TaskResult.Success();
    }

    /// <summary>The time between full-strength hits with an item.</summary>
    private static TimeSpan Cooldown(Item? weapon)
        => TimeSpan.FromSeconds(1 / (weapon ?? Item.Air).AttackSpeed());

    private bool InReach(Entity entity)
        => (player.Position + new Vector3d(0, Aim.EyeHeight, 0)).DistanceTo(entity.Center) <= Reach;

    /// <summary>
    /// The slot of the player window holding what hits hardest, or <c>null</c>
    /// when nothing carried beats the bare hand.
    /// </summary>
    private int? BestWeapon()
    {
        var best = inventory.Find(stack => stack.Item.AttackDamage() > 1)
            .OrderByDescending(found => found.Stack.Item.AttackDamage())
            .ThenBy(found => found.Slot == PlayerSlots.Hotbar(inventory.SelectedHotbarSlot) ? 0 : 1)
            .Select(found => (int?)found.Slot)
            .FirstOrDefault();

        return best;
    }
}
