namespace Vortex.Data;

/// <summary>
/// How long a block takes to break, and whether it drops anything, following
/// the game's rules.
/// </summary>
/// <remarks>
/// Only the rules and data are here, nothing about what the bot is holding: the
/// caller says which tool it is asking about.
/// </remarks>
public static class Mining
{
    /// <summary>
    /// How much a block's progress grows per tick, relative to its hardness,
    /// when the tool is right for it and when it is not.
    /// </summary>
    private const float CorrectToolDivisor = 30f;
    private const float IncorrectToolDivisor = 100f;

    /// <summary>
    /// Breaking is slowed to a fifth when not standing on the ground, and again
    /// when under water.
    /// </summary>
    private const float Penalty = 5f;

    /// <summary>
    /// The mining speed of a tool against a block: the first of its rules that
    /// covers the block and names a speed, otherwise its default.
    /// </summary>
    /// <param name="tool">The item in hand, or <c>null</c> for the bare hand.</param>
    public static float SpeedAgainst(Item? tool, Block block)
    {
        if (tool?.Tool() is not { } properties)
            return 1f;

        foreach (var rule in properties.Rules)
            if (rule.Speed is { } speed && rule.Blocks.Contains(block))
                return speed;

        return properties.DefaultMiningSpeed;
    }

    /// <summary>
    /// Whether mining a block with a tool drops its loot. Blocks that do not
    /// need a particular tool drop it with anything.
    /// </summary>
    public static bool CanHarvest(Block block, Item? tool)
        => !block.RequiresCorrectToolForDrops() || IsCorrectFor(tool, block);

    /// <summary>
    /// How many ticks breaking a block takes.
    /// </summary>
    /// <param name="tool">The item in hand, or <c>null</c> for the bare hand.</param>
    /// <param name="efficiency">The level of efficiency on the tool.</param>
    /// <param name="onGround">Whether the miner stands on the ground.</param>
    /// <param name="underwater">Whether the miner's head is under water.</param>
    /// <returns>
    /// The ticks, 0 for a block that breaks at once, or <c>null</c> for one that
    /// cannot be broken at all, such as bedrock.
    /// </returns>
    public static int? BreakTicks(Block block, Item? tool, int efficiency = 0, bool onGround = true, bool underwater = false)
    {
        var hardness = block.Hardness();

        if (hardness < 0)
            return null;

        if (hardness == 0)
            return 0;

        var speed = SpeedAgainst(tool, block);

        if (speed > 1 && efficiency > 0)
            speed += efficiency * efficiency + 1;

        if (underwater)
            speed /= Penalty;

        if (!onGround)
            speed /= Penalty;

        var progressPerTick = speed / hardness / (CanHarvest(block, tool) ? CorrectToolDivisor : IncorrectToolDivisor);

        // The block breaks on the tick its progress reaches 1.
        return progressPerTick >= 1 ? 0 : (int)Math.Ceiling(1 / progressPerTick);
    }

    private static bool IsCorrectFor(Item? tool, Block block)
    {
        if (tool?.Tool() is not { } properties)
            return false;

        foreach (var rule in properties.Rules)
            if (rule.CorrectForDrops is { } correct && rule.Blocks.Contains(block))
                return correct;

        return false;
    }
}
