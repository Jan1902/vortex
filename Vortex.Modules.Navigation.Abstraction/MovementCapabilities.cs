namespace Vortex.Modules.Navigation.Abstraction;

/// <summary>
/// What the player is allowed to do to get somewhere.
/// </summary>
/// <remarks>
/// <para>
/// This belongs to the search rather than to whatever carries the route out.
/// A gap can only be jumped if the search offered that move in the first place,
/// so turning an ability on or off here is what decides whether it is ever
/// planned. Whatever executes the route has to be able to do everything named
/// here, or it will be handed a route it cannot walk.
/// </para>
/// <para>
/// Passed per search rather than configured once, so that one task can ask for a
/// way there that touches nothing while another accepts whatever it takes.
/// </para>
/// </remarks>
/// <param name="JumpGaps">
/// Whether the player may jump across gaps in the floor. Costs nothing but the
/// risk of missing.
/// </param>
/// <param name="Diagonals">
/// Whether the player may move corner to corner rather than only along the
/// axes. Shorter routes, and fewer of the stops a staircase of single blocks
/// forces, at the price of a search with twice as much to look at.
/// </param>
/// <param name="Sprint">
/// Whether the player may sprint at a gap. Only ever used to reach a landing
/// walking cannot, because the faster the take-off the less say there is in
/// where it comes down.
/// </param>
public record MovementCapabilities(bool JumpGaps = false, bool Diagonals = false, bool Sprint = false)
{
    /// <summary>Walking only: no gap it cannot step across, nothing touched.</summary>
    public static MovementCapabilities Walking { get; } = new();

    /// <summary>Walking and jumping, the way a player crossing rough ground moves.</summary>
    public static MovementCapabilities Athletic { get; } = new(JumpGaps: true, Diagonals: true, Sprint: true);
}
