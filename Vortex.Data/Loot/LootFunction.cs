namespace Vortex.Data;

/// <summary>
/// A change a loot table makes to what drops, mostly to how many.
/// </summary>
/// <param name="Conditions">Conditions that must hold for the function to apply.</param>
public abstract record LootFunction(LootCondition[] Conditions);

/// <summary>Sets the count, or adds to it.</summary>
public sealed record SetCountFunction(LootNumber Count, bool Add, LootCondition[] Conditions) : LootFunction(Conditions);

/// <summary>Drops each item only by chance when the block is blown up.</summary>
public sealed record ExplosionDecayFunction(LootCondition[] Conditions) : LootFunction(Conditions);

/// <summary>Raises the count with the level of an enchantment such as fortune.</summary>
public sealed record ApplyBonusFunction(Enchantment Enchantment, BonusFormula Formula, LootCondition[] Conditions) : LootFunction(Conditions);

/// <summary>Keeps the count within bounds.</summary>
public sealed record LimitCountFunction(double? Min, double? Max, LootCondition[] Conditions) : LootFunction(Conditions);

/// <summary>Carries data such as a custom name over from the block onto the item.</summary>
public sealed record CopyComponentsFunction(DataComponentType[] Components, LootCondition[] Conditions) : LootFunction(Conditions);

/// <summary>Carries block state properties over onto the item, as a beehive keeps its honey.</summary>
public sealed record CopyStateFunction(Block Block, BlockProperty[] Properties, LootCondition[] Conditions) : LootFunction(Conditions);

/// <summary>
/// How <see cref="ApplyBonusFunction"/> turns the enchantment level into extra items.
/// </summary>
public abstract record BonusFormula;

/// <summary>Multiplies the count by a random factor that grows with the level, as for ores.</summary>
public sealed record OreDropsFormula : BonusFormula;

/// <summary>Adds up to <paramref name="BonusMultiplier"/> items per level.</summary>
public sealed record UniformBonusCountFormula(int BonusMultiplier) : BonusFormula;

/// <summary>Adds the successes of <c>level + Extra</c> tries with chance <paramref name="Probability"/>.</summary>
public sealed record BinomialWithBonusCountFormula(int Extra, double Probability) : BonusFormula;
