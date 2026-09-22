using Microsoft.Extensions.Logging;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Interaction.Abstraction;
using Vortex.Modules.Inventory.Abstraction;
using Vortex.Modules.Player.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Tasks;

/// <summary>
/// Opens a container, such as a chest, a furnace or a crafting table.
/// </summary>
/// <remarks>
/// Done while a container is open. The server does not say which block a
/// window belongs to, so any open container counts.
/// </remarks>
public class OpenContainerTask(
    Vector3i target,
    IInventoryManager inventory,
    IPlayerManager player,
    IInteractionManager interaction,
    Func<Vector3i, WithinReachTask> withinReach,
    ILogger<OpenContainerTask> logger) : BotTask
{
    /// <summary>How long the window has to open after the click.</summary>
    private static readonly TimeSpan OpenWait = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(20);

    public override string Description
        => $"open the container at {target.X} {target.Y} {target.Z}";

    public override bool IsSatisfied()
        => inventory.OpenContainer is not null;

    public override IEnumerable<BotTask> Dependencies()
    {
        yield return withinReach(target);
    }

    public override async Task<TaskResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        var face = await Aim.AtBlockAsync(player, target, cancellationToken);

        logger.LogDebug("Opening {Target}", target);

        await interaction.UseItemOnBlockAsync(target, face, cancellationToken: cancellationToken);

        var deadline = DateTime.UtcNow + OpenWait;

        while (!IsSatisfied())
        {
            if (DateTime.UtcNow > deadline)
                return TaskResult.Failed($"nothing opened at {target.X} {target.Y} {target.Z}");

            await Task.Delay(Poll, cancellationToken);
        }

        return TaskResult.Success();
    }
}
