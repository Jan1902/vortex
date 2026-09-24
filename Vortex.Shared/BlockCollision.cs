using System.Collections.Frozen;
using Vortex.Data;

namespace Vortex.Shared;

/// <summary>
/// Decides whether a block stops the player from moving through it.
/// </summary>
/// <remarks>
/// This is deliberately a simplification: every solid block is treated as a full
/// cube. Mojang's data reports do not contain collision shapes, those only exist
/// in the game's code, so slabs, stairs, fences and carpets are all full blocks
/// here. That is accurate enough to stand on the ground and walk across flat
/// terrain, and wrong for anything that needs exact shapes. Real collision
/// shapes are a separate piece of work.
/// </remarks>
/// <remarks>
/// Two things are known on top of that, for the pathfinder, because treating
/// those blocks as full cubes plans routes that cannot be walked: which blocks
/// are too tall to climb (<see cref="IsTall"/>), and which have their top too
/// far down to stand on as a full block (<see cref="HasLowTop"/>).
/// </remarks>
/// <remarks>
/// Lives here rather than in the player module because the pathfinder asks the
/// same question of the same block data, and the answer must not be allowed to
/// drift apart between the two.
/// </remarks>
public static class BlockCollision
{
    /// <summary>
    /// Blocks the player passes straight through: single blocks, and whole
    /// families taken from the game's own tags.
    /// </summary>
    private static readonly FrozenSet<Block> _passable = new[]
    {
        Block.Air, Block.CaveAir, Block.VoidAir,
        Block.Water, Block.Lava, Block.BubbleColumn,
        Block.Fire, Block.SoulFire,
        Block.ShortGrass, Block.TallGrass, Block.Fern, Block.LargeFern, Block.DeadBush,
        Block.Seagrass, Block.TallSeagrass, Block.Kelp, Block.KelpPlant, Block.SugarCane,
        Block.Vine, Block.GlowLichen, Block.Cobweb, Block.HangingRoots,
        Block.RedstoneWire, Block.Tripwire, Block.Lever, Block.Ladder, Block.Scaffolding,
        Block.Torch, Block.WallTorch, Block.SoulTorch, Block.SoulWallTorch,
        Block.RedstoneTorch, Block.RedstoneWallTorch,
        Block.BrownMushroom, Block.RedMushroom,
        Block.Wheat, Block.Carrots, Block.Potatoes, Block.Beetroots, Block.MelonStem, Block.PumpkinStem,
        Block.NetherWart, Block.SweetBerryBush, Block.CaveVines, Block.CaveVinesPlant,
        Block.Dandelion, Block.Poppy, Block.BlueOrchid, Block.Allium, Block.AzureBluet,
        Block.OxeyeDaisy, Block.Cornflower, Block.LilyOfTheValley, Block.WitherRose,
        Block.Torchflower, Block.PitcherPlant, Block.Sunflower, Block.Lilac, Block.RoseBush, Block.Peony,
        Block.RedTulip, Block.OrangeTulip, Block.WhiteTulip, Block.PinkTulip,
        Block.NetherPortal, Block.EndPortal, Block.EndGateway,
        Block.StructureVoid, Block.Light, Block.Snow,
        Block.MossCarpet, Block.BambooSapling,
        Block.DeadTubeCoral, Block.DeadBrainCoral, Block.DeadBubbleCoral, Block.DeadFireCoral, Block.DeadHornCoral,
        Block.DeadTubeCoralFan, Block.DeadBrainCoralFan, Block.DeadBubbleCoralFan, Block.DeadFireCoralFan, Block.DeadHornCoralFan,
        Block.DeadTubeCoralWallFan, Block.DeadBrainCoralWallFan, Block.DeadBubbleCoralWallFan, Block.DeadFireCoralWallFan, Block.DeadHornCoralWallFan,
    }
        .Concat(BlockTags.Buttons)
        .Concat(BlockTags.PressurePlates)
        .Concat(BlockTags.Rails)
        .Concat(BlockTags.Banners)
        .Concat(BlockTags.AllSigns)
        .Concat(BlockTags.WoolCarpets)
        .Concat(BlockTags.Candles)
        .Concat(BlockTags.Corals)
        .Concat(BlockTags.WallCorals)
        // Azaleas count as saplings, but are bushes the player bumps into.
        .Concat(BlockTags.Saplings.Where(block => block is not (Block.Azalea or Block.FloweringAzalea)))
        .ToFrozenSet();

    /// <summary>
    /// Blocks that stand taller than a block: a block and a half.
    /// </summary>
    private static readonly FrozenSet<Block> _tall = BlockTags.Fences
        .Concat(BlockTags.Walls)
        .Concat(BlockTags.FenceGates)
        .ToFrozenSet();

    /// <summary>
    /// Solid blocks whose top is well short of a full block: half a block or
    /// less, or not much more.
    /// </summary>
    /// <remarks>
    /// Only the ones where it matters. Blocks a sixteenth or two short of full,
    /// such as paths, farmland or soul sand, are near enough to a full block to
    /// stand on as one.
    /// </remarks>
    private static readonly FrozenSet<Block> _lowTop = new[]
    {
        Block.Repeater, Block.Comparator, Block.DaylightDetector,
        Block.Stonecutter, Block.EnchantingTable,
        Block.Cake,
        Block.SculkSensor, Block.CalibratedSculkSensor, Block.SculkShrieker,
        Block.LilyPad, Block.SeaPickle, Block.TurtleEgg,
        Block.Lantern, Block.SoulLantern, Block.FlowerPot,
        Block.Campfire, Block.SoulCampfire,
    }
        .Concat(BlockTags.Beds)
        .Concat(BlockTags.Trapdoors)
        .Concat(BlockTags.CandleCakes)
        .Concat(BlockTags.FlowerPots)
        .ToFrozenSet();

    /// <summary>
    /// Determines whether a block reaches higher than a full block, so that
    /// the player can neither step onto it nor jump over it.
    /// </summary>
    /// <remarks>
    /// Fences, walls and fence gates are a block and a half tall. Taken as full
    /// blocks, they look like something to hop onto, and the jump that would
    /// clear a block bumps into them instead.
    /// </remarks>
    public static bool IsTall(BlockState? state)
        => state is not null && _tall.Contains(state.Block);

    /// <summary>
    /// Determines whether a solid block's top is too far below a full block to
    /// be stood on as one.
    /// </summary>
    /// <remarks>
    /// A player on a bottom slab or a bed stands half a block lower than one on
    /// a full block. Treating it as full puts the player somewhere it is not.
    /// </remarks>
    public static bool HasLowTop(BlockState? state)
    {
        if (state is null)
            return false;

        if (BlockTags.Slabs.Contains(state.Block))
            return state.Get(BlockProperties.Type) == TypeValue.Bottom;

        return _lowTop.Contains(state.Block);
    }

    /// <summary>
    /// Determines whether the player collides with a block.
    /// </summary>
    /// <param name="state">The block, or <c>null</c> for a chunk that is not loaded.</param>
    /// <returns><c>true</c> if the block blocks movement.</returns>
    public static bool IsSolid(BlockState? state)
    {
        // Unloaded chunks are treated as solid so the player never falls through
        // a part of the world that simply has not arrived yet.
        if (state is null)
            return true;

        return !_passable.Contains(state.Block);
    }
}
