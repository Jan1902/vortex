using Vortex.Shared;

namespace Vortex.Modules.Navigation.Abstraction;

/// <summary>
/// One move along a route: where it ends up, and what the player has to do to
/// get there.
/// </summary>
/// <remarks>
/// <para>
/// A route is a list of these rather than a list of positions, because what the
/// search decided cannot always be read back out of the geometry. Two positions
/// either side of a gap say nothing about whether the plan was to jump it or to
/// bridge it, and a position inside a solid block is either a block to mine or a
/// bug. Naming the move removes the guesswork, and keeps the search and whatever
/// carries it out from drifting apart.
/// </para>
/// <para>
/// Every move ends on a block the player can stand on, so a route stays a
/// sequence of places to be, whatever happens in between.
/// </para>
/// </remarks>
/// <param name="To">The block the player stands on once the move is done.</param>
public abstract record Move(Vector3i To);

/// <summary>A step onto level ground next door.</summary>
public sealed record Walk(Vector3i To) : Move(To);

/// <summary>
/// A step onto a block one higher. Has to be walked into rather than aimed past,
/// so that the jump fires against it.
/// </summary>
public sealed record StepUp(Vector3i To) : Move(To);

/// <summary>
/// A step off an edge, landing some blocks below. Survivable without help.
/// </summary>
/// <param name="Height">How far the player falls, in blocks.</param>
public sealed record Drop(Vector3i To, int Height) : Move(To);

/// <summary>
/// A jump across a gap there is no floor in.
/// </summary>
/// <remarks>
/// Unlike a step up, this has to be committed to before the edge and needs the
/// run-up to already be there, so it is a movement of its own rather than
/// something that can be reacted to on contact. The landing may be level with
/// the take-off, a block above it or a couple below, and it may be reached
/// straight on or across a corner -- all of that is in where <c>To</c> is
/// relative to where the jump starts.
/// </remarks>
/// <param name="Distance">How many blocks of gap are being cleared.</param>
/// <param name="Sprinting">
/// Whether the run-up has to be a sprint. Only set where walking cannot reach,
/// because the faster the take-off the less say there is in where it lands.
/// </param>
public sealed record JumpGap(Vector3i To, int Distance, bool Sprinting = false) : Move(To);

/// <summary>
/// A step that only opens up once the blocks in the way have been taken out.
/// </summary>
/// <param name="Blocking">
/// The blocks to remove, in the order they should go, before the step can be
/// taken.
/// </param>
public sealed record MineThrough(Vector3i To, IReadOnlyList<Vector3i> Blocking) : Move(To);

/// <summary>
/// A step onto a block the player puts there itself, to cross something it
/// could not otherwise.
/// </summary>
/// <remarks>
/// Straight on only. The block goes against the side of the one the player is
/// standing on, placed from its edge.
/// </remarks>
/// <param name="Support">Where the block has to go.</param>
public sealed record Bridge(Vector3i To, Vector3i Support) : Move(To);

/// <summary>
/// Going straight up one block by jumping and placing a block underneath on
/// the way.
/// </summary>
/// <remarks>
/// The block goes where the player's feet were, on top of what it was standing
/// on, so <c>To</c> is always the block above the one the move starts from.
/// </remarks>
public sealed record Pillar(Vector3i To) : Move(To);

/// <summary>
/// Swimming along the surface of water to the next block, or into the water
/// from the bank.
/// </summary>
/// <remarks>
/// Only at the surface, head above water. Diving is not planned, and neither
/// is fighting a current.
/// </remarks>
public sealed record Swim(Vector3i To) : Move(To);

/// <summary>
/// A fall too long to survive, broken by placing water at the bottom and taking
/// it back afterwards.
/// </summary>
/// <param name="Height">How far the player falls, in blocks.</param>
public sealed record WaterDrop(Vector3i To, int Height) : Move(To);
