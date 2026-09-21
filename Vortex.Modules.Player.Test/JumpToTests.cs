using Vortex.Modules.Player.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Player.Test;

/// <summary>
/// The run-up, the take-off and what counts as having landed.
/// </summary>
/// <remarks>
/// These drive the controller against made-up states to pin down its
/// bookkeeping. What the jump actually does to the player is in
/// <see cref="LeapLandingTests"/>, which runs the real physics.
/// </remarks>
public class JumpToTests
{
    /// <summary>The floor is missing at x = 5, so the last solid block is x = 4.</summary>
    private const int Gap = 5;

    /// <summary>
    /// How far past the edge the player's feet still find ground, being wider
    /// than the block its middle is over.
    /// </summary>
    private const double Lip = 0.3;

    private static readonly Vector3d _start = new(2.5, Ticking.FloorTop, 0.5);
    private static readonly Vector3d _takeOff = new(Gap, Ticking.FloorTop, 0.5);
    private static readonly Vector3d _landing = new(Gap + 1.5, Ticking.FloorTop, 0.5);

    [Fact]
    public void WillNotStepUpMidLeap()
    {
        var controller = Leaping(out _);

        // Catching the lip of the gap would turn a planned jump into a scramble,
        // so the run-up may not climb either.
        Assert.False(controller.Tick(Ticking.At(_start)).AllowStepUp);
    }

    [Fact]
    public void TakesOffFromTheLastBlockBeforeTheGap()
    {
        var controller = Leaping(out _);

        // Not from halfway down the run-up: the jump belongs at the end of the
        // ground. The player is 0.6 wide, so its feet still carry it a little
        // past the block's edge before the ground truly runs out, and taking off
        // from there is the latest -- and longest -- jump available.
        Assert.InRange(RunUpTo(controller, _takeOff).X, Gap - 1, Gap + Lip);
    }

    [Fact]
    public void JumpsOnlyOnce()
    {
        var controller = Leaping(out _);

        RunUpTo(controller, _takeOff);

        var airborne = Ticking.At(new Vector3d(Gap + 0.2, Ticking.FloorTop + 0.42, 0.5), onGround: false);

        Assert.False(controller.Tick(airborne).Jump);
    }

    [Fact]
    public async Task DoesNotFinishWhileStillInTheAir()
    {
        var controller = Leaping(out var leap);

        RunUpTo(controller, _takeOff);

        controller.Tick(Ticking.At(new Vector3d(5.8, Ticking.FloorTop + 0.8, 0.5), onGround: false));
        controller.Tick(Ticking.At(new Vector3d(6.3, Ticking.FloorTop + 0.9, 0.5), onGround: false));

        Assert.False(leap.IsCompleted);

        Land(controller, new Vector3d(6.8, Ticking.FloorTop, 0.5));

        Assert.Equal(MovementResult.Arrived, await leap);
    }

    [Fact]
    public async Task OvershootingTheLandingStillCountsAsAcross()
    {
        var controller = Leaping(out var leap);

        RunUpTo(controller, _takeOff);

        // A jump cannot be made shorter once it has left the ground. Coming down
        // past the block it was aimed at is still across.
        Land(controller, new Vector3d(8.1, Ticking.FloorTop, 0.5));

        Assert.Equal(MovementResult.Arrived, await leap);
    }

    [Fact]
    public async Task FallingShortIsAMiss()
    {
        var controller = Leaping(out var leap);

        RunUpTo(controller, _takeOff);
        Land(controller, new Vector3d(5.4, Ticking.FloorTop, 0.5));

        Assert.Equal(MovementResult.Blocked, await leap);
    }

    [Fact]
    public async Task ComingDownBelowTheLandingIsAMiss()
    {
        var controller = Leaping(out var leap);

        RunUpTo(controller, _takeOff);

        // Far enough along, but at the bottom of the gap rather than on top of
        // the block.
        Land(controller, new Vector3d(7.0, Ticking.FloorTop - 4, 0.5));

        Assert.Equal(MovementResult.Blocked, await leap);
    }

    [Fact]
    public async Task AServerCorrectionEndsALeapAsDesynced()
    {
        var controller = Leaping(out var leap);

        RunUpTo(controller, _takeOff);
        controller.Desynchronize();

        Assert.Equal(MovementResult.Desynced, await leap);
    }

    /// <summary>
    /// A controller with a leap under way, run up to from <see cref="_start"/>.
    /// </summary>
    /// <remarks>
    /// The tick beforehand is what tells the controller where the run-up starts,
    /// which is what the direction of the whole jump is worked out from. In the
    /// bot that is simply the last tick of the physics loop.
    /// </remarks>
    private static MovementController Leaping(out Task<MovementResult> leap)
    {
        var controller = Ticking.Controller(new Gapped(Gap, Gap));

        controller.Tick(Ticking.At(_start));

        leap = controller.JumpTo(_takeOff, _landing);

        return controller;
    }

    /// <summary>
    /// Walks east a step at a time until the controller calls for the jump.
    /// </summary>
    /// <remarks>
    /// When exactly that happens is the controller's business, worked out by
    /// playing the jump out against the world, so a test that named the tick
    /// would be pinning down an answer rather than a question.
    /// </remarks>
    /// <returns>Where the player was standing when it went.</returns>
    private static Vector3d RunUpTo(MovementController controller, Vector3d edge)
    {
        var position = _start;

        while (position.X <= edge.X + Lip)
        {
            if (controller.Tick(Ticking.At(position)).Jump)
                return position;

            position = position with { X = position.X + 0.2 };
        }

        Assert.Fail($"never took off on the way to x = {edge.X + Lip:F1}");

        return position;
    }

    /// <summary>
    /// Flies the player to a point and puts it back on the ground there.
    /// </summary>
    /// <remarks>
    /// The tick in the air is not decoration. A leap ends on having come back
    /// down, which is a different thing from standing still -- otherwise a jump
    /// that never left the ground would report itself landed on the spot. So the
    /// player has to have been airborne for a landing to be one. The two ticks
    /// after it are the phase that was still steering and the coast behind it,
    /// each of which gets a tick of its own.
    /// </remarks>
    private static void Land(MovementController controller, Vector3d position)
    {
        controller.Tick(Ticking.At(position with { Y = position.Y + 1 }, onGround: false));

        controller.Tick(Ticking.At(position));
        controller.Tick(Ticking.At(position));
    }
}
