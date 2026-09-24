namespace Vortex.Modules.Navigation;

/// <summary>
/// How long a search may take, and how much it may look at.
/// </summary>
/// <remarks>
/// <para>
/// Measured in time rather than in positions, because what the bot cares about
/// is how long it stands still waiting for a plan. How many positions fit into
/// that depends on the terrain and the machine, not on anything the caller
/// knows.
/// </para>
/// <para>
/// Two limits, because there are two cases. When there is a way that gets
/// somewhere, the bot is better off walking the part of it that is known than
/// waiting for the rest, so <see cref="Patience"/> is short. When nothing
/// useful has turned up yet, it is worth waiting a little longer before saying
/// there is no way at all, which is <see cref="TimeLimit"/>.
/// </para>
/// </remarks>
/// <param name="Patience">
/// How long to look for the whole way before settling for part of it, if part
/// of it is known.
/// </param>
/// <param name="TimeLimit">How long to look at all.</param>
/// <param name="MaxExpandedPositions">
/// The most positions one search may look at, whatever the time. What stops a
/// search from filling the memory on a machine fast enough to look at millions
/// of them in the time allowed.
/// </param>
internal sealed record PathfinderOptions(TimeSpan Patience, TimeSpan TimeLimit, int MaxExpandedPositions)
{
    public static PathfinderOptions Default { get; } = new(
        Patience: TimeSpan.FromMilliseconds(500),
        TimeLimit: TimeSpan.FromSeconds(2),
        MaxExpandedPositions: 1_000_000);
}
