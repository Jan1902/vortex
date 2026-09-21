namespace Vortex.Modules.Navigation.Abstraction;

/// <summary>
/// How far the player can jump.
/// </summary>
/// <remarks>
/// <para>
/// The search cannot work this out for itself -- it knows about blocks, not
/// about gravity -- and the player cannot be asked at planning time without
/// simulating every candidate jump in the open set. So the answer lives here, as
/// the one place both sides agree on, and a test in the player module measures
/// the real physics against these numbers. If a jump ever stops carrying as far
/// as it says here, that test fails rather than the bot dropping into a hole.
/// </para>
/// <para>
/// Measured over flat ground with a clear run-up, which is what the search
/// checks for anyway. Falling buys distance -- there is more time in the air --
/// so a jump down carries further than a jump across, and a jump up carries
/// least of all.
/// </para>
/// </remarks>
public static class JumpReach
{
    /// <summary>The most the player may land above where it took off.</summary>
    public const int HighestLanding = 1;

    /// <summary>
    /// The most the player may land below where it took off on a jump.
    /// </summary>
    /// <remarks>
    /// Not a limit on falling -- a plain drop goes further -- but on how far
    /// down a landing can still be aimed at rather than simply fallen into.
    /// </remarks>
    public const int LowestLanding = -2;

    /// <summary>
    /// The furthest a landing may be from the take-off, measured centre to
    /// centre in blocks.
    /// </summary>
    /// <remarks>
    /// A distance rather than a count of blocks, so that it says as much about a
    /// jump across a corner as about one straight down a corridor.
    /// </remarks>
    /// <param name="sprinting">Whether the run-up is a sprint.</param>
    /// <param name="heightChange">
    /// How many blocks higher the landing is than the take-off; negative for a
    /// jump down.
    /// </param>
    /// <returns>The distance in blocks, or zero if such a jump is not on.</returns>
    public static double Furthest(bool sprinting, int heightChange)
        => heightChange > HighestLanding || heightChange < LowestLanding
            ? 0
            : WidestGap(sprinting, heightChange) + 1;

    /// <summary>
    /// The widest gap in the floor that can be crossed in a straight line, in
    /// blocks.
    /// </summary>
    /// <remarks>
    /// The form the numbers were measured in, and the easier one to check a
    /// corridor against. A gap of this many blocks puts the landing one block
    /// further out than the gap itself.
    /// </remarks>
    public static int WidestGap(bool sprinting, int heightChange)
        => (sprinting, heightChange) switch
        {
            // Rising eats the arc: the player is still climbing where it would
            // otherwise be travelling, and sprinting buys nothing back.
            (_, 1) => 2,

            (false, 0) => 2,
            (true, 0) => 3,

            (false, -1) => 3,
            (true, -1) => 3,

            (false, -2) => 3,
            (true, -2) => 4,

            _ => 0
        };
}
