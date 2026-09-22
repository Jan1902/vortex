using Microsoft.Extensions.Logging;
using Vortex.Data;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Interaction.Abstraction;
using Vortex.Modules.Inventory.Abstraction;
using Vortex.Modules.Player.Abstraction;
using Vortex.Modules.World.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Tasks;

/// <summary>
/// Places a block from the inventory at a position.
/// </summary>
/// <remarks>
/// <para>
/// A block is placed by using it on a side of a neighbouring block, so this
/// needs something solid next to the target -- below it if possible, as that
/// is what the target will rest on.
/// </para>
/// <para>
/// Done once the target holds the item's block. Which block an item places is
/// not in the game's data; items are matched to the block of the same name,
/// which holds for nearly all of them. For an item without one, such as
/// redstone, anything that fills the target counts.
/// </para>
/// </remarks>
public class PlaceBlockTask(
    Item item,
    Vector3i target,
    IWorldManager world,
    IPlayerManager player,
    IInventoryManager inventory,
    IInteractionManager interaction,
    Func<Vector3i, WithinReachTask> withinReach,
    ILogger<PlaceBlockTask> logger) : BotTask
{
    /// <summary>The player's width and height, which a block placed at the target must not overlap.</summary>
    private const double PlayerWidth = 0.6;
    private const double PlayerHeight = 1.8;

    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(50);

    /// <summary>How long the block has to appear after the server confirmed the click.</summary>
    private static readonly TimeSpan BlockUpdateWait = TimeSpan.FromSeconds(1);

    /// <summary>The sides to lean the new block against, below first.</summary>
    private static readonly BlockFace[] SupportOrder =
        [BlockFace.Down, BlockFace.North, BlockFace.South, BlockFace.West, BlockFace.East, BlockFace.Up];

    private readonly Block? _block = Enum.TryParse<Block>(item.ToString(), out var block) ? block : null;

    public override string Description
        => $"place {item} at {target.X} {target.Y} {target.Z}";

    public override bool IsSatisfied()
        => world.GetBlock(target) is { } state
            && (_block is { } expected ? state.Block == expected : !IsReplaceable(state.Block));

    public override IEnumerable<BotTask> Dependencies()
    {
        yield return withinReach(target);
    }

    public override async Task<TaskResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        if (world.GetBlock(target) is not { } state)
            return TaskResult.Failed($"{target.X} {target.Y} {target.Z} is not loaded");

        if (!IsReplaceable(state.Block))
            return TaskResult.Failed($"{state.Block} is already at {target.X} {target.Y} {target.Z}");

        if (Overlaps(player.Position))
            return TaskResult.Failed($"the bot is standing where {item} should go");

        if (FindSupport() is not { } support)
            return TaskResult.Failed($"nothing next to {target.X} {target.Y} {target.Z} to place {item} against");

        if (inventory.Find(stack => stack.Item == item).FirstOrDefault() is not { Stack: not null } found)
            return TaskResult.Failed($"the bot carries no {item}");

        if (!await Hold.InMainHandAsync(inventory, found.Slot))
            return TaskResult.Failed($"cannot get {item} into hand");

        // Clicking the side of the support that faces the target puts the block there.
        var (neighbour, face) = support;
        var offset = face.Offset();
        var cursor = new Vector3f(0.5f + offset.X * 0.5f, 0.5f + offset.Y * 0.5f, 0.5f + offset.Z * 0.5f);

        player.LookAt(BlockFaces.Center(neighbour, face));
        await Task.Delay(Tick, cancellationToken);

        logger.LogDebug("Placing {Item} at {Target} against the {Face} side of {Neighbour}", item, target, face, neighbour);

        if (!await interaction.UseItemOnBlockAsync(neighbour, face, cursor: cursor, cancellationToken: cancellationToken))
            return TaskResult.Failed($"the server did not confirm placing {item}");

        var deadline = DateTime.UtcNow + BlockUpdateWait;

        while (!IsSatisfied())
        {
            if (DateTime.UtcNow > deadline)
                return TaskResult.Failed($"the server did not place {item} at {target.X} {target.Y} {target.Z}");

            await Task.Delay(Tick, cancellationToken);
        }

        return TaskResult.Success();
    }

    /// <summary>
    /// A solid neighbour of the target, and the side of it that faces the target.
    /// </summary>
    private (Vector3i Neighbour, BlockFace Face)? FindSupport()
    {
        foreach (var side in SupportOrder)
        {
            var neighbour = target + side.Offset();

            if (BlockCollision.IsSolid(world.GetBlock(neighbour)) && world.GetBlock(neighbour) is not null)
                return (neighbour, side.Opposite());
        }

        return null;
    }

    /// <summary>Whether the player, standing at a position, takes up any of the target block.</summary>
    private bool Overlaps(Vector3d feet)
        => feet.X + PlayerWidth / 2 > target.X && feet.X - PlayerWidth / 2 < target.X + 1
            && feet.Y + PlayerHeight > target.Y && feet.Y < target.Y + 1
            && feet.Z + PlayerWidth / 2 > target.Z && feet.Z - PlayerWidth / 2 < target.Z + 1;

    private static bool IsReplaceable(Block block)
        => block is Block.Air or Block.CaveAir or Block.VoidAir || BlockTags.Replaceable.Contains(block);
}
