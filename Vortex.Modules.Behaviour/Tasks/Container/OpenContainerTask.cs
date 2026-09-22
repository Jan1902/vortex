using Microsoft.Extensions.Logging;
using Vortex.Data;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Tasks.Helper;
using Vortex.Modules.Behaviour.Tasks.Navigation;
using Vortex.Modules.Inventory.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Tasks.Container;

/// <summary>
/// Opens a container: a chest, a furnace, a crafting table.
/// </summary>
/// <remarks>
/// The server does not say which block a window belongs to, so this notes it
/// in the bot's chest memory, along with what is inside if it is storage.
/// </remarks>
public class OpenContainerTask(Vector3i target) : BotTask
{
    public override string Description
        => $"open the container at {target.X} {target.Y} {target.Z}";

    public override bool IsDone(Bot bot)
        => bot.Inventory.OpenContainer is not null && bot.Chests.OpenAt == target;

    public override async Task<TaskResult> RunAsync(Bot bot)
    {
        // Another one open would be taken for this one.
        if (bot.Inventory.OpenContainer is not null)
            await bot.Inventory.CloseContainerAsync();

        var reached = await bot.Run(new WithinReachTask(target));

        if (reached.IsFailure)
            return reached;

        var face = await Aim.AtBlockAsync(bot.Player, target, bot.Cancellation);

        bot.Logger.LogDebug("Opening the container at {X} {Y} {Z}", target.X, target.Y, target.Z);

        await bot.Interaction.UseItemOnBlockAsync(target, face, cancellationToken: bot.Cancellation);

        for (var waited = 0; waited < 20 && bot.Inventory.OpenContainer is null; waited++)
            await Task.Delay(50, bot.Cancellation);

        if (bot.Inventory.OpenContainer is null)
            return TaskResult.Failed($"nothing opened at {target.X} {target.Y} {target.Z}");

        bot.Chests.OpenAt = target;

        // The contents follow the window itself by a packet or two.
        await Task.Delay(200, bot.Cancellation);

        Remember(bot);

        return TaskResult.Success();
    }

    /// <summary>
    /// Notes what is in the open container, if it is one that stores things. A
    /// crafting table or a furnace opens the same way, but what lies in its
    /// slots is work in progress, not something to come back for.
    /// </summary>
    public static void Remember(Bot bot)
    {
        if (bot.Chests.OpenAt is not { } position || bot.Inventory.OpenContainer is not { } window)
            return;

        if (window.Type is not (Menu.Generic9x1 or Menu.Generic9x2 or Menu.Generic9x3 or Menu.Generic9x4
            or Menu.Generic9x5 or Menu.Generic9x6 or Menu.Generic3x3 or Menu.Hopper or Menu.ShulkerBox))
            return;

        bot.Chests.Remember(position, window.Slots.Take(window.ContainerSize));
    }
}
