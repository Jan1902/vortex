using Vortex.Modules.Player.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Player.Test;

/// <summary>
/// The take-off, the steering in the air and what counts as having landed.
/// </summary>
/// <remarks>
/// These drive the controller against made-up states to pin down its rules.
/// What a jump actually does to the player is in <see cref="LeapLandingTests"/>
/// and <see cref="JumpReachTests"/>, which run the real physics.
/// </remarks>
public class JumpToTests
{
    /// <summary>The ground ends at x = 5.</summary>
    private const int Edge = 5;

    private static readonly Vector3d _start = new(2.5, Ticking.FloorTop, 0.5);
    private static readonly Vector3d _takeOff = new(Edge, Ticking.FloorTop, 0.5);
    private static readonly Vector3d _landing = new(Edge + 1.5, Ticking.FloorTop, 0.5);

    [Fact]
    public void WillNotStepUpDuringALeap()
    {
        var controller = Leaping(out _);

        // Catching the lip of the gap would turn a planned jump into a scramble.
        Assert.False(controller.Tick(Ticking.At(_start)).AllowStepUp);
    }

    [Fact]
    public void AWalkingJumpLeavesAtTheEdge()
    {
        var controller = Leaping(out _);

        // The controlled jump: it goes as the ground runs out and does its aiming
        // in the air, by letting go.
        Assert.InRange(RunUp(controller).X, Edge - 0.1, Edge);
    }

    [Fact]
    public void ASprintingJumpLeavesAsLateAsItCan()
    {
        var controller = Leaping(out _, MovementMode.Sprint);

        // The jump for distance: the player's feet still carry it a little way
        // past the last block, and leaving from there spends the whole arc over
        // the gap.
        Assert.InRange(RunUp(controller).X, Edge + 0.2, Edge + 0.3);
    }

    [Fact]
    public void AWalkingJumpLetsGoOnceItsMomentumCarriesIt()
    {
        var controller = Leaping(out _);

        RunUp(controller);

        // Moving at walking speed with half a block to go: well within what the
        // speed alone carries, so pushing on would only overshoot.
        var drifting = Ticking.At(
            new Vector3d(_landing.X - 0.5, Ticking.FloorTop + 0.5, 0.5), new Vector3d(0.2158, 0, 0), onGround: false);

        Assert.Null(controller.Tick(drifting).Direction);
    }

    [Fact]
    public void JumpsOnlyOnce()
    {
        var controller = Leaping(out _);

        RunUp(controller);

        Assert.False(controller.Tick(Airborne(Edge + 0.5)).Jump);
    }

    [Fact]
    public void KeepsSteeringTowardsTheLandingInTheAir()
    {
        var controller = Leaping(out _);

        RunUp(controller);

        Assert.True(controller.Tick(Airborne(Edge + 0.5)).Direction!.X > 0);
    }

    [Fact]
    public void BrakesWhenItOvershoots()
    {
        var controller = Leaping(out _, MovementMode.Sprint);

        RunUp(controller);

        // Aimed afresh each tick, the push turns round once the player is past
        // the landing. That is what keeps a jump from carrying on off the far
        // side, and it is why nothing here has to be timed.
        Assert.True(controller.Tick(Airborne(_landing.X + 0.4)).Direction!.X < 0);
    }

    [Fact]
    public async Task DoesNotFinishWhileStillInTheAir()
    {
        var controller = Leaping(out var leap);

        RunUp(controller);

        controller.Tick(Airborne(5.8));
        controller.Tick(Airborne(6.3));

        Assert.False(leap.IsCompleted);

        controller.Tick(Ticking.At(new Vector3d(6.6, Ticking.FloorTop, 0.5)));

        Assert.Equal(MovementResult.Arrived, await leap);
    }

    [Fact]
    public async Task ComingDownOnTheNextBlockIsAMiss()
    {
        var controller = Leaping(out var leap);

        RunUp(controller);
        Land(controller, new Vector3d(7.4, Ticking.FloorTop, 0.5));

        // Unlike a drop, a jump is only planned where the landing itself is
        // good; what lies past it is not something the route vouched for.
        Assert.Equal(MovementResult.Blocked, await leap);
    }

    [Fact]
    public async Task FallingShortIsAMiss()
    {
        var controller = Leaping(out var leap);

        RunUp(controller);
        Land(controller, new Vector3d(5.4, Ticking.FloorTop, 0.5));

        Assert.Equal(MovementResult.Blocked, await leap);
    }

    [Fact]
    public async Task ComingDownBelowTheLandingIsAMiss()
    {
        var controller = Leaping(out var leap);

        RunUp(controller);

        // Far enough along, but at the bottom of the gap rather than on top of
        // the block.
        Land(controller, new Vector3d(6.5, Ticking.FloorTop - 4, 0.5));

        Assert.Equal(MovementResult.Blocked, await leap);
    }

    [Fact]
    public async Task AServerCorrectionEndsALeapAsDesynced()
    {
        var controller = Leaping(out var leap);

        RunUp(controller);
        controller.Desynchronize();

        Assert.Equal(MovementResult.Desynced, await leap);
    }

    /// <summary>
    /// A controller with a leap under way from <see cref="_start"/>.
    /// </summary>
    /// <remarks>
    /// The tick beforehand tells the controller where the player is, as the
    /// physics loop does in the bot.
    /// </remarks>
    private static MovementController Leaping(out Task<MovementResult> leap, MovementMode mode = MovementMode.Walk)
    {
        var controller = Ticking.Controller();

        controller.Tick(Ticking.At(_start));

        leap = controller.JumpTo(_takeOff, _landing, mode);

        return controller;
    }

    /// <summary>
    /// Walks east a tenth of a block at a time until the controller jumps.
    /// </summary>
    /// <returns>Where the player was when it went.</returns>
    private static Vector3d RunUp(MovementController controller)
    {
        for (var position = _start; position.X < Edge + 1; position = position with { X = position.X + 0.1 })
            if (controller.Tick(Ticking.At(position)).Jump)
                return position;

        Assert.Fail("never took off");

        return _start;
    }

    private static MovementState Airborne(double x)
        => Ticking.At(new Vector3d(x, Ticking.FloorTop + 0.8, 0.5), onGround: false);

    /// <summary>Flies the player to a point and puts it down there.</summary>
    private static void Land(MovementController controller, Vector3d position)
    {
        controller.Tick(Airborne(position.X));
        controller.Tick(Ticking.At(position));
    }
}
