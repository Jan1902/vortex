using Vortex.Modules.Player.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Player.Test;

/// <summary>
/// Where a jump actually puts the player, with the real controller driving the
/// real physics.
/// </summary>
/// <remarks>
/// The thing that went wrong once was not bookkeeping: a jump cleared the gap and
/// then carried a block too far, which on a ledge means straight off the far
/// side. Only running both halves together shows that.
/// </remarks>
public class LeapLandingTests
{
    private const int FloorTop = Ticking.FloorTop;
    private const int Edge = 10;

    [Theory]
    [InlineData(1, MovementMode.Walk)]
    [InlineData(2, MovementMode.Walk)]
    [InlineData(3, MovementMode.Sprint)]
    public async Task ComesDownOnTheBlockItWasAimedAt(int gap, MovementMode mode)
    {
        var world = new Gapped(Edge, Edge + gap - 1);
        var controller = Ticking.Controller();

        // From the middle of the last block, which is where a route starts it.
        var start = Ticking.At(new Vector3d(Edge - 0.5, FloorTop, 0.5));
        controller.Tick(start);

        var leap = controller.JumpTo(
            new Vector3d(Edge, FloorTop, 0.5),
            new Vector3d(Edge + gap + 0.5, FloorTop, 0.5),
            mode);

        var landed = Ticking.Walk(controller, world, start, leap);

        Assert.Equal(MovementResult.Arrived, await leap);
        Assert.Equal(new Vector3i(Edge + gap, FloorTop, 0), landed.Position.ToBlockPosition());
    }

    [Fact]
    public async Task ADiagonalJumpLandsOnItsBlock()
    {
        // Corner to corner across a trench, the way the route hands it over:
        // from the middle of one block, leaving at its corner, landing in the
        // middle of the block two across and two along. Steering at the landing
        // every tick needs nothing extra to go that way rather than along an
        // axis.
        var world = new Terrain(x => x == Edge ? null : FloorTop - 1);
        var controller = Ticking.Controller();

        var start = Ticking.At(new Vector3d(Edge - 0.5, FloorTop, 0.5));
        controller.Tick(start);

        var corner = Math.Sqrt(0.5) * 0.5;
        var takeOff = new Vector3d(Edge - 0.5 + corner, FloorTop, 0.5 + corner);
        var landing = new Vector3d(Edge + 1.5, FloorTop, 2.5);

        var leap = controller.JumpTo(takeOff, landing);

        var landed = Ticking.Walk(controller, world, start, leap);

        Assert.Equal(MovementResult.Arrived, await leap);
        Assert.Equal(landing.ToBlockPosition(), landed.Position.ToBlockPosition());
    }
}
