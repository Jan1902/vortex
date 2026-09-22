namespace Vortex.Data;

/// <summary>
/// What makes an item a tool: which blocks it mines faster, and which blocks
/// drop anything only when mined with it.
/// </summary>
/// <param name="Rules">Checked in order; the first rule that covers a block and says something decides.</param>
/// <param name="DefaultMiningSpeed">The speed against blocks no rule gives one for.</param>
public sealed record Tool(ToolRule[] Rules, float DefaultMiningSpeed = 1f);

/// <summary>
/// What a tool does to some blocks.
/// </summary>
/// <param name="Speed">The mining speed against them, or <c>null</c> to leave that to later rules.</param>
/// <param name="CorrectForDrops">Whether mining them with this tool drops their loot, or <c>null</c> to leave that to later rules.</param>
public sealed record ToolRule(IReadOnlySet<Block> Blocks, float? Speed, bool? CorrectForDrops);
