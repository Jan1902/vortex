using System.Collections.Frozen;
using Vortex.Data;

namespace Vortex.Shared;

/// <summary>
/// Decides whether a block will hurt the player that stands in or on it.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="BlockCollision"/> because it answers a different
/// question about the same data. Lava does not stop the player moving, so as far
/// as collision is concerned it is empty space; it is this class that knows
/// walking into it is fatal.
/// </para>
/// <para>
/// This matters more the more the bot can do. A bot that only walks gets away
/// with not knowing, because it cannot reach anywhere dangerous that it could
/// not also see. One that digs and takes long falls needs it, which is why this
/// comes before either of those.
/// </para>
/// </remarks>
public static class BlockHazard
{
    /// <summary>
    /// Blocks that damage the player for being in or on them.
    /// </summary>
    private static readonly FrozenSet<Block> _harmful = new[]
    {
        Block.Lava,
        Block.Fire, Block.SoulFire,
        Block.MagmaBlock,
        Block.Cactus,
        Block.SweetBerryBush,
        Block.WitherRose,
        Block.PowderSnow,
        Block.Campfire, Block.SoulCampfire,
        Block.EndPortal, Block.EndGateway, Block.NetherPortal,
    }.ToFrozenSet();

    /// <summary>
    /// Determines whether being at a block would hurt.
    /// </summary>
    /// <param name="state">The block, or <c>null</c> for a chunk that is not loaded.</param>
    /// <remarks>
    /// An unloaded chunk counts as safe: it is already treated as solid by
    /// <see cref="BlockCollision"/>, so nothing will be routed into it anyway,
    /// and calling it dangerous as well would only confuse the reason.
    /// </remarks>
    public static bool IsHarmful(BlockState? state)
        => state is not null && _harmful.Contains(state.Block);

    /// <summary>
    /// Determines whether the player would be underwater with its head in this
    /// block.
    /// </summary>
    /// <remarks>
    /// Swimming is not modelled: the physics treats water as empty space, so the
    /// bot walks along the bottom and drowns there. Wading through something
    /// ankle deep is fine, which is why this asks about the block at head height
    /// rather than about water anywhere.
    /// </remarks>
    public static bool Drowns(BlockState? state)
        => state?.Block is Block.Water or Block.BubbleColumn;
}
