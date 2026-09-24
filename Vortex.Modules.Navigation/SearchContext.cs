using Vortex.Data;
using Vortex.Modules.Navigation.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Navigation;

/// <summary>
/// What a movement needs to know to say whether it can be made: the world as
/// the search sees it, what the player is allowed to do, and what it carries.
/// </summary>
/// <remarks>
/// The rules every movement shares about the terrain live here, so that a
/// step up and a jump agree on what counts as room to stand.
/// </remarks>
internal sealed class SearchContext(BlockCache blocks, MovementCapabilities allowed)
{
    /// <summary>What breaking each kind of block costs with what the player carries, worked out once each.</summary>
    private readonly Dictionary<Block, double?> _breakCosts = [];

    public MovementCapabilities Allowed
        => allowed;

    /// <summary>
    /// Whether the player fits at a position and would survive being there: two
    /// blocks of room, a floor to stand on, and nothing that hurts.
    /// </summary>
    public bool CanStandAt(Cell position)
        => blocks.CanStandAt(position);

    /// <summary>
    /// Whether the player can float at a position: in still water, with its
    /// head above the surface.
    /// </summary>
    /// <remarks>
    /// Still water only. Flowing water carries the player off, which is a
    /// current to fight rather than a place to be.
    /// </remarks>
    public bool CanFloatAt(Cell position)
    {
        var head = position.Above;

        return blocks.IsStillWater(position)
            && blocks.IsPassable(head)
            && !blocks.IsWater(head)
            && !blocks.IsHarmful(head);
    }

    /// <summary>Whether a position is one the player floats at rather than stands at.</summary>
    public bool IsFloating(Cell position)
        => !blocks.CanStandAt(position) && CanFloatAt(position);

    /// <summary>
    /// Whether a position is one the player can be at, standing or floating.
    /// </summary>
    public bool CanBeAt(Cell position)
        => blocks.CanStandAt(position) || CanFloatAt(position);

    /// <summary>Whether there is room for the player's body in a block.</summary>
    public bool IsPassable(Cell position)
        => blocks.IsPassable(position);

    /// <summary>Whether a block holds the player up at its top.</summary>
    public bool IsFloor(Cell position)
        => blocks.IsFloor(position);

    /// <summary>Whether a block is solid, which is what a placed block can lean against.</summary>
    public bool IsSolid(Cell position)
        => blocks.IsSolid(position);

    /// <summary>Whether a block has to be broken before the player can be in it.</summary>
    public bool NeedsBreaking(Cell position)
        => blocks.NeedsBreaking(position);

    public bool IsWater(Cell position)
        => blocks.IsWater(position);

    /// <summary>
    /// Whether a block can be put where this one is, replacing it: air, a
    /// liquid, or the likes of tall grass. Not a flower, which has to be broken
    /// first.
    /// </summary>
    public bool IsReplaceable(Cell position)
        => blocks.BlockAt(position) is Block.Air or Block.CaveAir or Block.VoidAir or Block.Water or Block.Lava
        || BlockTags.Replaceable.Contains(blocks.BlockAt(position));

    public bool IsHarmful(Cell position)
        => blocks.IsHarmful(position);

    public bool Drowns(Cell position)
        => blocks.Drowns(position);

    /// <summary>Whether a position is obstructed at either body height.</summary>
    public bool IsBlocked(Cell position)
        => !blocks.IsPassable(position) || !blocks.IsPassable(position.Above);

    /// <summary>Whether standing at a position would damage the player.</summary>
    public bool IsSafeAt(Cell position)
        => blocks.IsSafeAt(position);

    /// <summary>Whether there is room to jump from a position: the block over the head is clear.</summary>
    public bool HasHeadroom(Cell position)
        => blocks.IsPassable(position.Above.Above);

    /// <summary>
    /// Whether a position is open at body height but has nothing to stand on:
    /// somewhere to fall or jump across rather than walk.
    /// </summary>
    public bool IsGap(Cell position)
        => !blocks.CanStandAt(position) && !IsBlocked(position);

    /// <summary>
    /// Whether a diagonal step has room to go round the corner.
    /// </summary>
    /// <remarks>
    /// Both of the blocks either side of the corner have to be clear at body
    /// height. Standing on them is not required -- cutting across the corner of
    /// a hole is exactly what a diagonal is for -- but squeezing between two
    /// walls is not.
    /// </remarks>
    public bool CornerIsClear(Cell at, Cell direction)
        => !IsBlocked(at + new Cell(direction.X, 0, 0))
        && !IsBlocked(at + new Cell(0, 0, direction.Z));

    /// <summary>
    /// Whether a block may be broken to get through.
    /// </summary>
    /// <remarks>
    /// Bedrock and the like cannot be broken at all, and neither can anything
    /// that would take longer than <see cref="Costs.MaxBreakTicks"/> with what
    /// the player carries. Next to a liquid is a bad idea, because what comes
    /// through the hole does not stop, and so is underneath sand or gravel,
    /// which falls into the hole and fills it again.
    /// </remarks>
    public bool CanBreak(Cell position)
    {
        if (!blocks.IsBreakable(position) || blocks.IsHarmful(position) || blocks.Drowns(position))
            return false;

        if (BreakCost(position) is null)
            return false;

        return !IsLeaky(position.Above)
            && !IsLeaky(position.Below)
            && !IsLeaky(position + new Cell(1, 0, 0))
            && !IsLeaky(position + new Cell(-1, 0, 0))
            && !IsLeaky(position + new Cell(0, 0, 1))
            && !IsLeaky(position + new Cell(0, 0, -1))
            && !blocks.Falls(position.Above);
    }

    /// <summary>
    /// What breaking a block costs, in ticks: the time it takes with the best
    /// of what the player carries, and a flat charge on top. Null for one that
    /// takes too long or cannot be broken at all.
    /// </summary>
    public double? BreakCost(Cell position)
    {
        var block = blocks.BlockAt(position);

        if (_breakCosts.TryGetValue(block, out var known))
            return known;

        double? cost = allowed.Loadout.BreakTicks(block) is { } ticks && ticks <= Costs.MaxBreakTicks
            ? ticks + Costs.BreakPenalty
            : null;

        _breakCosts[block] = cost;

        return cost;
    }

    /// <summary>
    /// What breaking a list of blocks costs, or null if any of them cannot be
    /// broken.
    /// </summary>
    public double? BreakCost(BlockList toBreak)
    {
        var total = 0.0;

        for (var i = 0; i < toBreak.Count; i++)
        {
            if (!CanBreak(toBreak[i]) || BreakCost(toBreak[i]) is not { } cost)
                return null;

            total += cost;
        }

        return total;
    }

    /// <summary>
    /// Whether a player standing at a position could see any part of a block.
    /// </summary>
    /// <remarks>
    /// The player's own body is never in the way. Where the search gets to a
    /// position by digging, the world does not know yet that the two blocks it
    /// stands in are gone, and they must not hide what it has dug its way to.
    /// </remarks>
    public bool CanSee(Cell standing, Cell target)
    {
        var eyes = new Vector3d(standing.X + 0.5, standing.Y + LineOfSight.EyeHeight, standing.Z + 0.5);
        var head = standing.Above;

        return LineOfSight.Sight(eyes, target.ToVector3i(), position =>
        {
            Cell cell = position;

            return cell != standing && cell != head && blocks.IsSolid(cell);
        }) is not null;
    }

    /// <summary>Whether a block would pour into a hole opened next to it.</summary>
    private bool IsLeaky(Cell position)
        => blocks.Drowns(position) || blocks.IsHarmful(position);
}
