namespace Vortex.Modules.Navigation.Movements;

/// <summary>
/// One kind of move the search can plan, such as a walk, a jump or a tunnel.
/// </summary>
/// <remarks>
/// <para>
/// Each kind decides for itself where it can go from a block and what that
/// costs. The search only asks all of them in turn, so a new ability -- placing
/// a block to bridge a gap, say -- is a new implementation of this and nothing
/// else.
/// </para>
/// <para>
/// The same code answers both of the questions there are about a move: while
/// searching, where the player could go from here; while walking the route,
/// whether the move it planned can still be made. Asking the two separately is
/// how a route ends up offering a move that its own checks would refuse.
/// </para>
/// </remarks>
internal interface IMovement
{
    /// <summary>Adds every move of this kind that can be made from a block.</summary>
    /// <param name="context">The world as the search sees it, and what is allowed.</param>
    /// <param name="from">The block the player stands on.</param>
    /// <param name="into">Where to add the moves.</param>
    void Expand(SearchContext context, Cell from, List<Step> into);
}
