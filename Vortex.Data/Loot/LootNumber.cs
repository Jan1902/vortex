namespace Vortex.Data;

/// <summary>
/// A number in a loot table, such as how many items drop, which may be random.
/// </summary>
public abstract record LootNumber
{
    /// <summary>The smallest value it can take.</summary>
    public abstract double Minimum { get; }

    /// <summary>The largest value it can take.</summary>
    public abstract double Maximum { get; }
}

/// <summary>Always the same number.</summary>
public sealed record ConstantLootNumber(double Value) : LootNumber
{
    public override double Minimum => Value;

    public override double Maximum => Value;
}

/// <summary>Any number between two bounds, all equally likely.</summary>
public sealed record UniformLootNumber(double Min, double Max) : LootNumber
{
    public override double Minimum => Min;

    public override double Maximum => Max;
}

/// <summary>The number of successes in <paramref name="N"/> tries with chance <paramref name="P"/> each.</summary>
public sealed record BinomialLootNumber(int N, double P) : LootNumber
{
    public override double Minimum => 0;

    public override double Maximum => N;
}
