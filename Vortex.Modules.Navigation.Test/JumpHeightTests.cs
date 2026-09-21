using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Modules.Navigation.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Navigation.Test;

/// <summary>
/// Jumps that do not land where they took off from.
/// </summary>
/// <remarks>
/// Jumping up onto a ledge across a gap, and down onto one below, are the same
/// movement as jumping across: the run-up, the take-off and the letting go are
/// all the same. What changes is how far the arc carries, which is why the
/// search prices and limits them separately rather than treating anything
/// off-level as out of the question.
/// </remarks>
public class JumpHeightTests
{
    [Fact]
    public void JumpsUpOntoALedgeAcrossAGap()
    {
        var route = Find(width: 1, farSide: 1);

        Assert.NotNull(route);

        var jump = Assert.Single(route.Moves.OfType<JumpGap>());

        Assert.Equal(65, jump.To.Y);
    }

    [Fact]
    public void JumpsDownOntoALedgeAcrossAGap()
    {
        var route = Find(width: 2, farSide: -1);

        Assert.NotNull(route);

        var jump = Assert.Single(route.Moves.OfType<JumpGap>());

        Assert.Equal(63, jump.To.Y);
    }

    [Fact]
    public void WillNotJumpUpHigherThanItCanReach()
    {
        // Two blocks up across a gap is not a jump, it is a wish. Nothing to
        // stand on within reach means no route, which is the honest answer.
        Assert.Null(Find(width: 1, farSide: 2));
    }

    [Fact]
    public void ARiseCostsReach()
    {
        // The arc is spent climbing where it would otherwise be travelling, so
        // the gap that is comfortable on the level is beyond it going up.
        var across = Find(width: 3, farSide: 0);
        var up = Find(width: 3, farSide: 1);

        Assert.NotNull(across);
        Assert.Null(up);
    }

    /// <summary>
    /// A route across a corridor with the floor missing for a run of blocks and
    /// nothing at all below it, so the far side can only be reached through the
    /// air, sitting a number of blocks above or below the near one.
    /// </summary>
    /// <remarks>
    /// The goal stands on the far side, so it moves with it. Aiming at a fixed
    /// height would be asking to stand inside the floor as soon as the far side
    /// rises, and the search would refuse for that reason rather than for the
    /// jump.
    /// </remarks>
    private static Route? Find(int width, int farSide)
    {
        var world = new FakeWorld()
            .WithFloor(63, -4, 4, -1, 1)
            .WithFloor(63 + farSide, 5 + width, 14, -1, 1);

        return new AStarPathfinder(world, NullLogger<AStarPathfinder>.Instance)
            .FindRoute(
                new Vector3i(0, 64, 0),
                new Vector3i(12, 64 + farSide, 0),
                MovementCapabilities.Athletic);
    }
}
