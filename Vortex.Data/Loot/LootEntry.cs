namespace Vortex.Data;

/// <summary>
/// One of the things a loot pool can hand out.
/// </summary>
/// <param name="Conditions">Conditions that must hold for the entry to be chosen.</param>
public abstract record LootEntry(LootCondition[] Conditions)
{
    /// <summary>
    /// Adds the items this entry can produce in a context.
    /// </summary>
    /// <returns>Whether the entry is certain to produce something, which ends a list of alternatives.</returns>
    internal abstract bool CollectPossibleDrops(LootContext context, ISet<Item> drops);
}

/// <summary>Drops an item.</summary>
public sealed record ItemLootEntry(Item Item, LootCondition[] Conditions, LootFunction[] Functions) : LootEntry(Conditions)
{
    internal override bool CollectPossibleDrops(LootContext context, ISet<Item> drops)
    {
        if (!Conditions.CanAllPass(context))
            return false;

        drops.Add(Item);

        return !Conditions.CanAnyFail(context);
    }
}

/// <summary>
/// Drops what the first of its children whose conditions hold drops, as stone
/// drops itself with silk touch and cobblestone otherwise.
/// </summary>
public sealed record AlternativesLootEntry(LootEntry[] Children, LootCondition[] Conditions) : LootEntry(Conditions)
{
    internal override bool CollectPossibleDrops(LootContext context, ISet<Item> drops)
    {
        if (!Conditions.CanAllPass(context))
            return false;

        // A child is reached only while every one before it may still fail, so
        // stop at the first that is certain to be taken.
        foreach (var child in Children)
            if (child.CollectPossibleDrops(context, drops))
                return !Conditions.CanAnyFail(context);

        return false;
    }
}

/// <summary>
/// Drops what the block itself holds, which only the block entity knows, such
/// as the sherds a decorated pot was made from.
/// </summary>
public sealed record DynamicLootEntry(DynamicLootContents Contents, LootCondition[] Conditions) : LootEntry(Conditions)
{
    internal override bool CollectPossibleDrops(LootContext context, ISet<Item> drops)
        => false;
}

/// <summary>
/// What a <see cref="DynamicLootEntry"/> drops, as the game's code names it.
/// </summary>
public enum DynamicLootContents
{
    /// <summary><c>minecraft:contents</c>: the items stored inside, as in a shulker box.</summary>
    Contents,

    /// <summary><c>minecraft:sherds</c>: the sherds a decorated pot was made from.</summary>
    Sherds
}
