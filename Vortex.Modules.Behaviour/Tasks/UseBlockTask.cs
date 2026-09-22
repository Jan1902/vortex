using Microsoft.Extensions.Logging;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Interaction.Abstraction;
using Vortex.Modules.Player.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Tasks;

/// <summary>
/// Uses a block once, with whatever is in hand: flips a lever, presses a button,
/// opens a door.
/// </summary>
/// <remarks>
/// Using is an act, not a state of the world: a lever flipped twice is where it
/// started, and nothing in the world says whether this task already did its
/// part. So unlike other tasks it remembers one thing -- that the server
/// confirmed the use. A task whose goal is a state, such as a container being
/// open, should look at that state instead of building on this.
/// </remarks>
public class UseBlockTask(
    Vector3i target,
    IPlayerManager player,
    IInteractionManager interaction,
    Func<Vector3i, WithinReachTask> withinReach,
    ILogger<UseBlockTask> logger) : BotTask
{
    private bool _used;

    public override string Description
        => $"use the block at {target.X} {target.Y} {target.Z}";

    public override bool IsSatisfied()
        => _used;

    public override IEnumerable<BotTask> Dependencies()
    {
        yield return withinReach(target);
    }

    public override async Task<TaskResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        var face = await Aim.AtBlockAsync(player, target, cancellationToken);

        logger.LogDebug("Using the {Face} side of {Target}", face, target);

        if (!await interaction.UseItemOnBlockAsync(target, face, cancellationToken: cancellationToken))
            return TaskResult.Failed($"the server did not confirm using {target.X} {target.Y} {target.Z}");

        _used = true;

        return TaskResult.Success();
    }
}
