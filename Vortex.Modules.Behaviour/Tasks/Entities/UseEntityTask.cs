using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Tasks.Blocks;
using Vortex.Modules.Behaviour.Tasks.Helper;
using Vortex.Modules.Behaviour.Tasks.Navigation;
using Vortex.Modules.Entities.Abstraction;
using Vortex.Modules.Interaction.Abstraction;
using Vortex.Modules.Navigation.Abstraction;
using Vortex.Modules.Player.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Tasks.Entities;

/// <summary>
/// Uses an entity once with whatever is in hand: trades with a villager, milks a
/// cow, gets into a boat.
/// </summary>
/// <remarks>
/// Like <see cref="UseBlockTask"/> this is an act rather than a state, so it
/// remembers having done it; there is no reply from the server to wait for.
/// </remarks>
public class UseEntityTask(
    int entityId,
    IEntityManager entities,
    IPlayerManager player,
    IInteractionManager interaction,
    Func<Vector3i, MovementCapabilities, GoToTask> goTo) : BotTask
{
    /// <summary>How close the entity has to be to use it, from the eyes to its middle.</summary>
    private const double Reach = 3.0;

    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(50);

    private bool _used;

    public override string Description
        => $"use entity {entityId}";

    public override bool IsSatisfied()
        => _used;

    public override IEnumerable<BotTask> Dependencies()
    {
        if (entities.Get(entityId) is { } entity
            && (player.Position + new Vector3d(0, Aim.EyeHeight, 0)).DistanceTo(entity.Center) > Reach)
            yield return goTo(entity.Position.ToBlockPosition(), MovementCapabilities.Athletic);
    }

    public override async Task<TaskResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        if (entities.Get(entityId) is not { } entity)
            return TaskResult.Failed($"entity {entityId} is gone");

        player.LookAt(entity.Center);
        await Task.Delay(Tick, cancellationToken);

        await interaction.InteractWithEntityAsync(entityId);

        _used = true;

        return TaskResult.Success();
    }
}
