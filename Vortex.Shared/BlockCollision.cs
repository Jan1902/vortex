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
    /// Blocks the player passes straight through.
    /// </summary>
    private static readonly HashSet<string> _passable =
    [
        "air", "cave_air", "void_air",
        "water", "lava", "bubble_column",
        "fire", "soul_fire",
        "short_grass", "grass", "tall_grass", "fern", "large_fern", "dead_bush",
        "seagrass", "tall_seagrass", "kelp", "kelp_plant", "sugar_cane",
        "vine", "glow_lichen", "cobweb", "hanging_roots",
        "redstone_wire", "tripwire", "lever", "ladder", "scaffolding",
        "torch", "wall_torch", "soul_torch", "soul_wall_torch",
        "redstone_torch", "redstone_wall_torch",
        "brown_mushroom", "red_mushroom",
        "wheat", "carrots", "potatoes", "beetroots", "melon_stem", "pumpkin_stem",
        "nether_wart", "sweet_berry_bush", "cave_vines", "cave_vines_plant",
        "dandelion", "poppy", "blue_orchid", "allium", "azure_bluet",
        "oxeye_daisy", "cornflower", "lily_of_the_valley", "wither_rose",
        "torchflower", "pitcher_plant", "sunflower", "lilac", "rose_bush", "peony",
        "red_tulip", "orange_tulip", "white_tulip", "pink_tulip",
        "nether_portal", "end_portal", "end_gateway",
        "structure_void", "light", "snow",
    ];

    /// <summary>
    /// Name endings shared by whole families of passable blocks.
    /// </summary>
    private static readonly string[] _passableSuffixes =
    [
        "_sapling", "_rail", "_button", "_pressure_plate", "_banner",
        "_sign", "_carpet", "_candle", "_coral_fan", "_coral_wall_fan",
    ];

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

        var name = StripNamespace(state.BlockName);

        if (_passable.Contains(name))
            return false;

        foreach (var suffix in _passableSuffixes)
            if (name.EndsWith(suffix, StringComparison.Ordinal))
                return false;

        return true;
    }

    private static string StripNamespace(string blockName)
    {
        var separator = blockName.IndexOf(':');

        return separator < 0 ? blockName : blockName[(separator + 1)..];
    }
}
