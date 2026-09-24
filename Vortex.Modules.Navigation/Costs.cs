namespace Vortex.Modules.Navigation;

/// <summary>
/// What the moves the search plans cost, in ticks.
/// </summary>
/// <remarks>
/// Everything the search prices is measured in ticks, because as soon as it can
/// mine or bridge its way through, it has to answer "round or through", and
/// that is a question about time. Mixing a count of blocks with a mining time in
/// one number would have it tunnel through a mountain because that came out
/// three blocks shorter.
/// </remarks>
internal static class Costs
{
    /// <summary>What one block of walking costs.</summary>
    public const double Walk = 1 / 0.2158;

    /// <summary>
    /// What getting onto a block one higher costs.
    /// </summary>
    /// <remarks>
    /// Measured from the physics: walking into the block, jumping, and coming
    /// down on top of it takes about eight ticks from the moment of take-off.
    /// Nearly twice a plain walk, which is what makes the search prefer a flat
    /// way round when there is one going spare.
    /// </remarks>
    public const double StepUp = 9.0;

    /// <summary>What stepping off an edge costs before the fall itself.</summary>
    public const double Drop = 5.0;

    /// <summary>
    /// What a jump across a gap costs: the arc itself, plus a little for the
    /// run-up it has to be taken at.
    /// </summary>
    public const double JumpGap = 14.0;

    /// <summary>
    /// What a jump taken at a sprint costs.
    /// </summary>
    /// <remarks>
    /// Dearer than the same jump walked, though it takes no longer. The extra is
    /// not time, it is margin: the faster the take-off, the less say there is in
    /// where the player comes down, so a route that sprints where it could have
    /// walked is taking a risk for nothing.
    /// </remarks>
    public const double SprintJump = 18.0;

    /// <summary>
    /// What breaking a block costs on top of the time it takes.
    /// </summary>
    /// <remarks>
    /// The time comes from the block and the best tool the player carries. This
    /// is the rest: turning to it, switching tools, waiting for the server --
    /// and reluctance. Without it a player with a good pickaxe would find
    /// tunnelling through stone nearly as cheap as walking, and cut straight
    /// through every hill on the way.
    /// </remarks>
    public const double BreakPenalty = 20.0;

    /// <summary>
    /// The longest breaking one block may take, in ticks, for it to be worth
    /// planning at all.
    /// </summary>
    /// <remarks>
    /// Ten seconds. Stone by hand takes less; obsidian without a diamond
    /// pickaxe, or a cobweb without a sword, takes far more, and walking round
    /// is the better idea however far round is.
    /// </remarks>
    public const int MaxBreakTicks = 200;

    /// <summary>
    /// What a block of height still to lose adds to the estimate when there is
    /// no digging: a little, because a drop gets down a block for about the
    /// price of the walk it also covers, and a long jump down for less.
    /// </summary>
    public const double Descend = 1.0;

    /// <summary>
    /// What a block of height still to lose adds to the estimate when digging
    /// is allowed: the least digging down a block can cost, the step down and
    /// the flat charge for breaking, without the breaking itself.
    /// </summary>
    /// <remarks>
    /// A guess rather than a bound, because where the ground slopes or there
    /// is a cave, going down is far cheaper than this. That only makes those
    /// ways look better than digging, which they are.
    /// </remarks>
    public const double DigDownEstimate = Drop + BreakPenalty;

    /// <summary>
    /// What one block of swimming at the surface costs: water holds the player
    /// to about half its walking speed.
    /// </summary>
    public const double Swim = 10.0;

    /// <summary>
    /// What placing a block costs on top of the movement it is part of.
    /// </summary>
    /// <remarks>
    /// Partly the time to aim and place, mostly reluctance: every block placed
    /// is one fewer to place later and one more left behind in the world, so a
    /// way that needs none is worth a fair detour.
    /// </remarks>
    public const double Place = 20.0;

    /// <summary>
    /// What going up a block by jumping and placing one underneath costs.
    /// </summary>
    public const double Pillar = StepUp + Place;

    /// <summary>
    /// What stepping across a gap onto a block placed in it costs: sneaking to
    /// the edge, placing, and walking on.
    /// </summary>
    public const double Bridge = 1 / 0.0653 + Place;

    /// <summary>Charged per block of gap, so the shortest jump that works wins.</summary>
    public const double JumpBlock = 2.0;

    /// <summary>
    /// Charged per block of the fall beyond the first. Falling accelerates, so
    /// each further block takes less time than the one before it; this is the
    /// flat approximation of that.
    /// </summary>
    public const double FallPerBlock = 2.0;

    /// <summary>
    /// What a block of height still to climb adds to the estimate, on top of
    /// the walk across.
    /// </summary>
    /// <remarks>
    /// Every block up takes at least one step up, and a step up is the cheapest
    /// way there is to gain height: it covers one block of ground as well, which
    /// the horizontal part of the estimate already counts as a walk. What is
    /// left over is the difference. Going down is not counted at all, because a
    /// long jump down can come out cheaper than walking the same ground.
    /// </remarks>
    public const double Climb = StepUp - Walk;

    /// <summary>
    /// Charged for changing direction, which is what makes the search prefer a
    /// few long legs to a staircase of the same length.
    /// </summary>
    /// <remarks>
    /// <para>
    /// With every direction costing exactly the same, a great many routes are
    /// tied on price and the search is free to return any of them, including one
    /// that zigzags across open ground. That costs nothing to walk, but it
    /// cannot be covered in one movement, so the bot ends up stopping at every
    /// single block.
    /// </para>
    /// <para>
    /// Small enough that it only ever settles a tie: it would take a thousand
    /// turns to outweigh one extra block, so no route is ever made longer in
    /// order to make it straighter. Written as a fraction of the walk so that it
    /// stays that way if the walk is ever re-measured.
    /// </para>
    /// </remarks>
    public const double Turn = Walk / 1000;

    /// <summary>
    /// What a move onto a block of the route being replaced is charged, as a
    /// share of its real cost.
    /// </summary>
    /// <remarks>
    /// Searching again halfway along a route should not swap it for a different
    /// one of the same price at every turn: that has the bot wobbling between two
    /// ways round a hill without taking either. Going back onto the old route
    /// for half price keeps it on course unless the new way is clearly better.
    /// </remarks>
    public const double FavourPrevious = 0.5;
}
