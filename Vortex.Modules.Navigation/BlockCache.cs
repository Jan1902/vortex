using Vortex.Data;
using Vortex.Modules.World.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Navigation;

/// <summary>
/// What one search knows about the blocks it has looked at, read from the world
/// once each and kept for the rest of the search.
/// </summary>
/// <remarks>
/// <para>
/// The search asks about the same few blocks over and over: every position is
/// looked at from each of its neighbours, and every question about standing
/// somewhere is three blocks at once. Going to the world for each of those takes
/// its lock every time, which is most of what a search used to spend.
/// </para>
/// <para>
/// Kept per search rather than shared, which is also what makes it a snapshot:
/// a block that changes halfway through a search does not leave it with half an
/// old world and half a new one. Keeping up with the world is the job of
/// whatever walks the route, which checks it again before every move.
/// </para>
/// <para>
/// Stored a chunk section at a time, sixteen blocks cubed, because that is how
/// the search moves through the world: a block's neighbours are nearly always
/// in the same section, and the last one used is kept to hand.
/// </para>
/// </remarks>
internal sealed class BlockCache(IWorldManager world)
{
    private const int SectionVolume = 16 * 16 * 16;

    private readonly Dictionary<long, Slot[]> _sections = [];

    private Slot[]? _lastSection;
    private long _lastKey;

    /// <summary>What is known about one block.</summary>
    [Flags]
    private enum Facts : ushort
    {
        /// <summary>Set once the block has been read at all; nothing else means anything before that.</summary>
        Read = 1 << 0,
        Solid = 1 << 1,
        Harmful = 1 << 2,
        Drowns = 1 << 3,
        Unbreakable = 1 << 4,

        /// <summary>Sand or gravel, which falls into whatever is dug out under it.</summary>
        Falls = 1 << 5,

        /// <summary>Set once whether the player can stand here has been worked out.</summary>
        StandingKnown = 1 << 6,
        Standable = 1 << 7,

        /// <summary>A fence, wall or gate: taller than a block, so the block above it is taken as well.</summary>
        Tall = 1 << 8,

        /// <summary>Solid, but with its top well short of a full block, so no floor to stand on.</summary>
        LowTop = 1 << 9,

        /// <summary>A cobweb: nothing to bump into, but it all but stops whoever walks in.</summary>
        Web = 1 << 10,

        /// <summary>Water of any kind, flowing or still.</summary>
        Water = 1 << 11,

        /// <summary>Still water, a source block, which stays put and can be swum on.</summary>
        StillWater = 1 << 12,
    }

    /// <summary>What is known about one block, and which block it is.</summary>
    private struct Slot
    {
        public Facts Facts;
        public Block Block;
    }

    /// <summary>
    /// Whether there is room for the player's body in a block: nothing solid,
    /// no cobweb, and no fence or wall reaching up into it from below.
    /// Unloaded counts as solid.
    /// </summary>
    public bool IsPassable(Cell position)
        => (Read(position) & (Facts.Solid | Facts.Web)) == 0
        && (Read(position.Below) & Facts.Tall) == 0;

    /// <summary>Whether a block is solid, which is what the physics collides with.</summary>
    public bool IsSolid(Cell position)
        => (Read(position) & Facts.Solid) != 0;

    /// <summary>
    /// Whether a block holds the player up at its top: solid, and neither too
    /// low nor too tall to stand on as a full block.
    /// </summary>
    public bool IsFloor(Cell position)
        => (Read(position) & (Facts.Solid | Facts.Tall | Facts.LowTop)) == Facts.Solid;

    /// <summary>Whether a block has to be broken before the player can be in it.</summary>
    public bool NeedsBreaking(Cell position)
        => (Read(position) & (Facts.Solid | Facts.Web)) != 0;

    /// <summary>Whether a block is water of any kind.</summary>
    public bool IsWater(Cell position)
        => (Read(position) & Facts.Water) != 0;

    /// <summary>Whether a block is still water, which can be swum on.</summary>
    public bool IsStillWater(Cell position)
        => (Read(position) & Facts.StillWater) != 0;

    /// <summary>Which block is at a position, for the rules that care which one it is.</summary>
    public Block BlockAt(Cell position)
    {
        Read(position);

        return Entry(position).Block;
    }

    /// <summary>Whether being in or on a block hurts.</summary>
    public bool IsHarmful(Cell position)
        => (Read(position) & Facts.Harmful) != 0;

    /// <summary>Whether a head in this block is under water.</summary>
    public bool Drowns(Cell position)
        => (Read(position) & Facts.Drowns) != 0;

    /// <summary>Whether a block could be broken at all, leaving aside what is around it.</summary>
    public bool IsBreakable(Cell position)
        => (Read(position) & Facts.Unbreakable) == 0;

    /// <summary>Whether a block falls when what holds it up is taken away.</summary>
    public bool Falls(Cell position)
        => (Read(position) & Facts.Falls) != 0;

    /// <summary>
    /// Whether the player fits at a position and would survive being there: two
    /// blocks of room, something solid to stand on, and nothing that hurts.
    /// </summary>
    /// <remarks>
    /// Asked more than anything else in a search, so the answer is kept with the
    /// block the feet are in. Hazards are checked on the two blocks the body
    /// occupies and the one it stands on, because magma burns through boots
    /// while lava and fire burn what is in them. Drowning is asked about head
    /// height only, so that wading through something shallow stays allowed.
    /// </remarks>
    public bool CanStandAt(Cell position)
    {
        ref var slot = ref Entry(position);

        if ((slot.Facts & Facts.StandingKnown) != 0)
            return (slot.Facts & Facts.Standable) != 0;

        var head = position.Above;
        var below = position.Below;

        var standable = IsPassable(position)
            && IsPassable(head)
            && IsFloor(below)
            && IsSafeAt(position);

        // Looked up again: reading the neighbours may have moved on to another
        // section and the reference is only good for the one it came from.
        ref var again = ref Entry(position);

        again.Facts |= Facts.StandingKnown | (standable ? Facts.Standable : 0);

        return standable;
    }

    /// <summary>Whether standing at a position would damage the player.</summary>
    public bool IsSafeAt(Cell position)
    {
        var head = position.Above;

        return !IsHarmful(position)
            && !IsHarmful(head)
            && !IsHarmful(position.Below)
            && !Drowns(head);
    }

    private Facts Read(Cell position)
    {
        ref var slot = ref Entry(position);

        if ((slot.Facts & Facts.Read) == 0)
        {
            var state = world.GetBlock(position.ToVector3i());

            slot.Facts |= Describe(state);
            slot.Block = state?.Block ?? Block.Air;
        }

        return slot.Facts;
    }

    private static Facts Describe(BlockState? state)
    {
        var facts = Facts.Read;

        if (BlockCollision.IsSolid(state))
            facts |= Facts.Solid;

        // An unloaded block is solid and harmless, as BlockCollision and
        // BlockHazard see it, and cannot be broken: there is nothing there yet.
        if (state is null)
            return facts | Facts.Unbreakable;

        if (BlockHazard.IsHarmful(state))
            facts |= Facts.Harmful;

        if (BlockHazard.Drowns(state))
            facts |= Facts.Drowns;

        if (state.Block.Hardness() < 0)
            facts |= Facts.Unbreakable;

        if (state.Block is Block.Sand or Block.RedSand or Block.Gravel)
            facts |= Facts.Falls;

        if (BlockCollision.IsTall(state))
            facts |= Facts.Tall;

        if (BlockCollision.HasLowTop(state))
            facts |= Facts.LowTop;

        if (state.Block == Block.Cobweb)
            facts |= Facts.Web;

        if (state.Block == Block.Water)
            facts |= state.Get(BlockProperties.Level) is 0 or null ? Facts.Water | Facts.StillWater : Facts.Water;

        return facts;
    }

    private ref Slot Entry(Cell position)
    {
        var key = SectionKey(position);

        if (_lastSection is null || key != _lastKey)
        {
            if (!_sections.TryGetValue(key, out var section))
            {
                section = new Slot[SectionVolume];
                _sections[key] = section;
            }

            _lastSection = section;
            _lastKey = key;
        }

        return ref _lastSection[(position.X & 15) | (position.Z & 15) << 4 | (position.Y & 15) << 8];
    }

    /// <summary>
    /// The section a block is in, packed into one number: 24 bits each for the
    /// section's x and z, which covers the whole world and then some, and 16 for
    /// its y.
    /// </summary>
    private static long SectionKey(Cell position)
        => ((long)((position.X >> 4) & 0xFFFFFF) << 40)
        | ((long)((position.Z >> 4) & 0xFFFFFF) << 16)
        | (long)((position.Y >> 4) & 0xFFFF);
}
