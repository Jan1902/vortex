using Microsoft.Extensions.Logging;
using Vortex.Data;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Tasks.Helper;
using Vortex.Modules.Behaviour.Tasks.Navigation;
using Vortex.Modules.Interaction.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Tasks.Blocks;

/// <summary>
/// Places a block from the inventory at a position.
/// </summary>
/// <remarks>
/// A block is placed by using it on a side of a neighbouring block, so this
/// needs something solid next to the target, below it if possible. Items are
/// matched to the block of the same name; for an item without one, such as
/// redstone, anything that fills the target counts.
/// </remarks>
public class PlaceBlockTask(Item item, Vector3i target) : BotTask
{
    private const double PlayerWidth = 0.6;
    private const double PlayerHeight = 1.8;

    /// <summary>The sides to lean the new block against, below first.</summary>
    private static readonly BlockFace[] SupportOrder =
        [BlockFace.Down, BlockFace.North, BlockFace.South, BlockFace.West, BlockFace.East, BlockFace.Up];

    private readonly Block? _block = Enum.TryParse<Block>(item.ToString(), out var block) ? block : null;

    public override string Description
        => $"place {item} at {target.X} {target.Y} {target.Z}";

    public override bool IsDone(Bot bot)
        => bot.World.GetBlock(target) is { } state
            && (_block is { } expected ? state.Block == expected : !IsReplaceable(state.Block));

    public override async Task<TaskResult> RunAsync(Bot bot)
    {
        var reached = await bot.Run(new WithinReachTask(target));

        if (reached.IsFailure)
            return reached;

        if (bot.World.GetBlock(target) is not { } state)
            return TaskResult.Failed($"{target.X} {target.Y} {target.Z} is not loaded");

        if (!IsReplaceable(state.Block))
            return TaskResult.Failed($"{state.Block} is already at {target.X} {target.Y} {target.Z}");

        if (Overlaps(bot.Player.Position))
            return TaskResult.Failed($"the bot is standing where {item} should go");

        if (FindSupport(bot) is not { } support)
            return TaskResult.Failed($"nothing next to {target.X} {target.Y} {target.Z} to place {item} against");

        if (bot.Inventory.Find(stack => stack.Item == item).FirstOrDefault() is not { Stack: not null } found)
            return TaskResult.Failed($"the bot carries no {item}");

        if (!await Hold.InMainHandAsync(bot.Inventory, found.Slot))
            return TaskResult.Failed($"cannot get {item} into hand");

        var (neighbour, face) = support;
        var offset = face.Offset();
        var cursor = new Vector3f(0.5f + offset.X * 0.5f, 0.5f + offset.Y * 0.5f, 0.5f + offset.Z * 0.5f);

        bot.Player.LookAt(BlockFaces.Center(neighbour, face));
        await Task.Delay(50, bot.Cancellation);

        bot.Logger.LogDebug("Placing {Item} against the {Face} side of {Neighbour}", item, face, neighbour);

        if (!await bot.Interaction.UseItemOnBlockAsync(neighbour, face, cursor: cursor, cancellationToken: bot.Cancellation))
            return TaskResult.Failed($"the server did not let the bot place {item}");

        for (var waited = 0; waited < 20 && !IsDone(bot); waited++)
            await Task.Delay(50, bot.Cancellation);

        return IsDone(bot)
            ? TaskResult.Success()
            : TaskResult.Failed($"the server did not place {item} at {target.X} {target.Y} {target.Z}");
    }

    private (Vector3i Neighbour, BlockFace Face)? FindSupport(Bot bot)
    {
        foreach (var side in SupportOrder)
        {
            var neighbour = target + side.Offset();

            if (bot.World.GetBlock(neighbour) is { } state && BlockCollision.IsSolid(state))
                return (neighbour, side.Opposite());
        }

        return null;
    }

    private bool Overlaps(Vector3d feet)
        => feet.X + PlayerWidth / 2 > target.X && feet.X - PlayerWidth / 2 < target.X + 1
            && feet.Y + PlayerHeight > target.Y && feet.Y < target.Y + 1
            && feet.Z + PlayerWidth / 2 > target.Z && feet.Z - PlayerWidth / 2 < target.Z + 1;

    internal static bool IsReplaceable(Block block)
        => block is Block.Air or Block.CaveAir or Block.VoidAir || BlockTags.Replaceable.Contains(block);
}
