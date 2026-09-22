namespace Vortex.Data;

/// <summary>
/// A condition in a loot table that decides whether a pool, entry or function
/// applies.
/// </summary>
/// <remarks>
/// Some conditions are down to chance, and some depend on things the context may
/// not know. A condition therefore answers two questions instead of one: whether
/// it can hold, and whether it can fail. Both are true for a coin toss; exactly
/// one is for a condition that is settled.
/// </remarks>
public abstract record LootCondition
{
    /// <summary>Whether the condition can hold in this context.</summary>
    public abstract bool CanPass(LootContext context);

    /// <summary>Whether the condition can fail in this context.</summary>
    public abstract bool CanFail(LootContext context);
}

/// <summary>
/// Holds unless the block is destroyed by an explosion, which then only
/// sometimes lets the drop survive.
/// </summary>
public sealed record SurvivesExplosionCondition : LootCondition
{
    public override bool CanPass(LootContext context) => true;

    public override bool CanFail(LootContext context) => context.Explosion;
}

/// <summary>
/// Holds when the broken block's state has particular property values, such as
/// wheat being fully grown.
/// </summary>
/// <param name="Matches">Tests the properties of a state of <paramref name="Block"/>.</param>
public sealed record BlockStatePropertyCondition(Block Block, Func<BlockState, bool> Matches) : LootCondition
{
    public override bool CanPass(LootContext context) => context.State is null || Holds(context.State);

    public override bool CanFail(LootContext context) => context.State is null || !Holds(context.State);

    private bool Holds(BlockState state) => state.Block == Block && Matches(state);
}

/// <summary>
/// Holds when the block is broken with a particular tool, or with one carrying
/// an enchantment, such as shears or anything with silk touch.
/// </summary>
/// <param name="Items">The tools that qualify, or <c>null</c> if any does.</param>
/// <param name="Enchantment">An enchantment the tool must carry, if any.</param>
/// <param name="MinimumLevel">The level the enchantment must have at least.</param>
public sealed record MatchToolCondition(IReadOnlySet<Item>? Items, Enchantment? Enchantment, int MinimumLevel) : LootCondition
{
    public override bool CanPass(LootContext context) => Holds(context);

    public override bool CanFail(LootContext context) => !Holds(context);

    private bool Holds(LootContext context)
        => (Items is null || context.Tool is { } tool && Items.Contains(tool))
            && (Enchantment is not { } enchantment || context.LevelOf(enchantment) >= MinimumLevel);
}

/// <summary>
/// Holds by chance, with the chance growing with the level of an enchantment
/// such as fortune.
/// </summary>
/// <param name="Chances">The chance at level 0, 1, 2 and so on; the last one applies beyond.</param>
public sealed record TableBonusCondition(Enchantment Enchantment, double[] Chances) : LootCondition
{
    public override bool CanPass(LootContext context) => Chance(context) > 0;

    public override bool CanFail(LootContext context) => Chance(context) < 1;

    private double Chance(LootContext context) => Chances[Math.Min(context.LevelOf(Enchantment), Chances.Length - 1)];
}

/// <summary>Holds by chance.</summary>
public sealed record RandomChanceCondition(double Chance) : LootCondition
{
    public override bool CanPass(LootContext context) => Chance > 0;

    public override bool CanFail(LootContext context) => Chance < 1;
}

/// <summary>Holds when any of its terms does.</summary>
public sealed record AnyOfCondition(LootCondition[] Terms) : LootCondition
{
    public override bool CanPass(LootContext context) => Terms.Any(term => term.CanPass(context));

    public override bool CanFail(LootContext context) => Terms.All(term => term.CanFail(context));
}

/// <summary>Holds when its term does not.</summary>
public sealed record InvertedCondition(LootCondition Term) : LootCondition
{
    public override bool CanPass(LootContext context) => Term.CanFail(context);

    public override bool CanFail(LootContext context) => Term.CanPass(context);
}

/// <summary>
/// Holds when a block next to the broken one is a particular block, as the
/// other half of a tall plant.
/// </summary>
/// <param name="Matches">Further tests the neighbour's state, or <c>null</c> if any state will do.</param>
public sealed record LocationCheckCondition(int OffsetX, int OffsetY, int OffsetZ, IReadOnlySet<Block> Blocks, Func<BlockState, bool>? Matches) : LootCondition
{
    public override bool CanPass(LootContext context) => context.Neighbour is null || Holds(context);

    public override bool CanFail(LootContext context) => context.Neighbour is null || !Holds(context);

    private bool Holds(LootContext context)
        => context.Neighbour!(OffsetX, OffsetY, OffsetZ) is { } neighbour
            && Blocks.Contains(neighbour.Block)
            && (Matches is null || Matches(neighbour));
}

/// <summary>
/// Holds when the block is broken by an entity, a player for one, rather than
/// by something like an explosion.
/// </summary>
public sealed record EntityPropertiesCondition : LootCondition
{
    public override bool CanPass(LootContext context) => !context.Explosion;

    public override bool CanFail(LootContext context) => context.Explosion;
}

/// <summary>
/// Evaluates a list of conditions, all of which have to hold.
/// </summary>
internal static class LootConditions
{
    public static bool CanAllPass(this LootCondition[] conditions, LootContext context)
        => conditions.All(condition => condition.CanPass(context));

    public static bool CanAnyFail(this LootCondition[] conditions, LootContext context)
        => conditions.Any(condition => condition.CanFail(context));
}
