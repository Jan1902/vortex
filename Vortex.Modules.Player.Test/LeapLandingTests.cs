using Vortex.Modules.Player.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Player.Test;

/// <summary>
/// Where a planned jump actually puts the player, with the real controller
/// driving the real physics.
/// </summary>
/// <remarks>
/// The other jump tests check the controller's bookkeeping against made-up
/// states. These run the whole loop, because the thing that went wrong was not
/// bookkeeping: the jump cleared the gap and then carried a block too far, and
/// only running both halves together shows that.
/// </remarks>
public class LeapLandingTests
{
    private const int FloorTop = Ticking.FloorTop;
    private const int Edge = 10;

    [Theory]
    [InlineData(1, MovementMode.Walk)]
    [InlineData(2, MovementMode.Walk)]
    [InlineData(1, MovementMode.Sprint)]
    [InlineData(2, MovementMode.Sprint)]
    public void ComesDownOnTheBlockItWasAimedAt(int gap, MovementMode mode)
    {
        var landingBlock = Edge + gap;
        var flight = Fly(gap, mode);

        Assert.Equal(MovementResult.Arrived, flight.Result);

        // Holding the run-up direction through the whole arc used to land a one
        // block gap at x = 12.4, a full block past the block it was aimed at,
        // which on a ledge means straight off the far side.
        Assert.InRange(flight.Position.X, landingBlock, landingBlock + 1);
        Assert.Equal(FloorTop, flight.Position.Y, precision: 3);
    }

    [Fact]
    public void LetsGoOnceTheMomentumAlreadyCarriesIt()
    {
        // Well into the arc, fast, with the landing close: holding on from here
        // sails the player over the block it was aimed at and off the far side.
        var airborne = Ticking.At(
            new Vector3d(10.5, FloorTop + 1.0, 0.5), new Vector3d(0.2806, -0.1, 0), onGround: false);

        Assert.Null(MidFlight(Edge + 1.5, MovementMode.Sprint, airborne).Direction);
    }

    [Fact]
    public void KeepsSteeringAllTheWayWhenItNeedsEveryBlock()
    {
        // Just off the ground, walking, with the landing three blocks out:
        // letting go here drops the player into the gap.
        var airborne = Ticking.At(
            new Vector3d(10.0, FloorTop + 0.42, 0.5), new Vector3d(0.2158, 0.42, 0), onGround: false);

        Assert.NotNull(MidFlight(Edge + 3.0, MovementMode.Walk, airborne).Direction);
    }

    /// <summary>
    /// Takes a leap off the ground and then puts the player mid-arc by hand, to
    /// ask what it would hold down from there.
    /// </summary>
    /// <remarks>
    /// The take-off itself is left to the controller -- when to go is its own
    /// decision, worked out against the world -- and only what happens after it
    /// is dictated here.
    /// </remarks>
    private static MovementInput MidFlight(double landingX, MovementMode mode, MovementState airborne)
    {
        var controller = Ticking.Controller(new Gapped(Edge, Edge));

        var position = new Vector3d(7.5, FloorTop, 0.5);

        controller.Tick(Ticking.At(position));

        controller.JumpTo(
            new Vector3d(Edge, FloorTop, 0.5), new Vector3d(landingX, FloorTop, 0.5), mode);

        while (position.X <= Edge + 0.3)
        {
            if (controller.Tick(Ticking.At(position)).Jump)
                return controller.Tick(airborne);

            position = position with { X = position.X + 0.2 };
        }

        Assert.Fail("never took off");

        return MovementInput.Idle;
    }

    /// <summary>What came of one run-up, jump and landing.</summary>
    private readonly record struct Flight(
        Vector3d Position,
        MovementResult Result,
        int SteeredWhileAirborne,
        int CoastedWhileAirborne);

    /// <summary>
    /// Runs east along a floor with a gap in it, jumps it, and reports where the
    /// player ended up and how it flew.
    /// </summary>
    private static Flight Fly(int gap, MovementMode mode = MovementMode.Walk)
    {
        var world = new Gapped(Edge, Edge + gap - 1);
        var controller = Ticking.Controller(world);
        var physics = new PlayerPhysics(world);

        var state = Ticking.At(new Vector3d(0.5, FloorTop, 0.5));

        // The tick before the jump is asked for is what tells the controller
        // where the run-up starts, as the physics loop does in the bot.
        controller.Tick(state);

        var leap = controller.JumpTo(
            new Vector3d(Edge, FloorTop, 0.5),
            new Vector3d(Edge + gap + 0.5, FloorTop, 0.5),
            mode);

        var steered = 0;
        var coasted = 0;

        for (var tick = 0; tick < 300 && !leap.IsCompleted; tick++)
        {
            var input = controller.Tick(state);

            if (!state.OnGround)
            {
                if (input.Direction is null)
                    coasted++;
                else
                    steered++;
            }

            var step = physics.Step(state.Position, state.Velocity, state.OnGround, input);

            state = new MovementState(step.Position, step.Velocity, step.OnGround, step.Blocked);
        }

        Assert.True(leap.IsCompleted, "the leap never finished");

        return new Flight(state.Position, leap.Result, steered, coasted);
    }

}
