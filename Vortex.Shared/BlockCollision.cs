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
