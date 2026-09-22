using Vortex.Modules.Player.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Player.Test;

public class MovementControllerTests
{
    /// <summary>A point that many blocks east of the origin, where these start.</summary>
    private static Vector3d East(double blocks)
        => new(blocks, 0, 0);

    [Fact]
    public async Task ArrivesAfterCoveringTheDistance()
    {
        var controller = Ticking.Controller();
        var movement = controller.WalkTo(East(3));

        var position = Vector3d.Zero;

        for (var tick = 0; tick < 40 && !movement.IsCompleted; tick++)
        {
            controller.Tick(Ticking.At(position));
            position = position with { X = position.X + 0.2 };
        }

        Assert.Equal(MovementResult.Arrived, await movement);
        Assert.False(controller.IsMoving);
    }

    [Fact]
    public async Task ReportsBlockedWhenItStopsMakingProgress()
    {
        var controller = Ticking.Controller();
        var movement = controller.WalkTo(East(10));

        // The player never moves, which is what running into a wall looks like.
        for (var tick = 0; tick < 40 && !movement.IsCompleted; tick++)
            controller.Tick(Ticking.At(Vector3d.Zero, blocked: true));

        Assert.Equal(MovementResult.Blocked, await movement);
    }

    [Fact]
    public async Task ReportsBlockedWhenItGrindsAlongWithoutProgress()
    {
        var controller = Ticking.Controller();
        var movement = controller.WalkTo(East(10));

        var position = Vector3d.Zero;

        // Moving, but sideways: no collision is reported, yet the target never
        // gets closer. This is the case a pure collision check would miss.
        for (var tick = 0; tick < 60 && !movement.IsCompleted; tick++)
        {
            controller.Tick(Ticking.At(position));
            position = position with { Z = position.Z + 0.2 };
        }

        Assert.Equal(MovementResult.Blocked, await movement);
    }

    [Fact]
    public async Task GivesUpOnAMovementThatNeverEnds()
    {
        var controller = Ticking.Controller();
        var movement = controller.WalkTo(East(1000));

        var position = Vector3d.Zero;

        // Making ground the whole time, so nothing reads as stuck -- but a
        // thousand blocks is not one movement, and something has to end it.
        for (var tick = 0; tick <= MovementController.Timeout + 1 && !movement.IsCompleted; tick++)
        {
            controller.Tick(Ticking.At(position));
            position = position with { X = position.X + 0.2 };
        }

        Assert.Equal(MovementResult.Blocked, await movement);
    }

    [Fact]
    public async Task StoppingCancelsTheMovement()
    {
        var controller = Ticking.Controller();
        var movement = controller.WalkTo(East(10));

        controller.Stop();

        Assert.Equal(MovementResult.Cancelled, await movement);
        Assert.False(controller.IsMoving);
    }

    [Fact]
    public async Task ANewMovementCancelsThePreviousOne()
    {
        var controller = Ticking.Controller();

        var first = controller.WalkTo(East(10));
        var second = controller.WalkTo(new Vector3d(0, 0, 10));

        Assert.Equal(MovementResult.Cancelled, await first);
        Assert.False(second.IsCompleted);
    }

    [Fact]
    public void PassesTheModeAndDirectionThrough()
    {
        var controller = Ticking.Controller();
        controller.WalkTo(East(5), MovementMode.Sprint);

        var input = controller.Tick(Ticking.At(Vector3d.Zero));

        Assert.NotNull(input.Direction);
        Assert.Equal(1, input.Direction!.X, precision: 6);
        Assert.Equal(MovementMode.Sprint, input.Mode);
    }

    [Fact]
    public void WalksWhereverItIsPointed()
    {
        var controller = Ticking.Controller();

        // Nothing here is axis-aligned: a direction is a direction, which is
        // what lets the route plan diagonals without this having to learn them.
        controller.WalkTo(new Vector3d(5, 0, 5));

        var direction = controller.Tick(Ticking.At(Vector3d.Zero)).Direction;

        Assert.NotNull(direction);
        Assert.Equal(Math.Sqrt(0.5), direction!.X, precision: 6);
        Assert.Equal(Math.Sqrt(0.5), direction.Z, precision: 6);
    }

    [Fact]
    public void JumpIsRequestedOnceAndThenCleared()
    {
        var controller = Ticking.Controller();
        controller.Jump();

        Assert.True(controller.Tick(Ticking.At(Vector3d.Zero)).Jump);
        Assert.False(controller.Tick(Ticking.At(Vector3d.Zero)).Jump);
    }

    [Fact]
    public void AutoJumpTriesOnceWhenBlocked()
    {
        var controller = Ticking.Controller();
        controller.WalkTo(East(10), autoJump: true);

        Assert.False(controller.Tick(Ticking.At(Vector3d.Zero)).Jump);
        Assert.True(controller.Tick(Ticking.At(Vector3d.Zero, blocked: true)).Jump);

        // Only one attempt per obstacle, otherwise a wall turns into endless hopping.
        Assert.False(controller.Tick(Ticking.At(Vector3d.Zero, blocked: true)).Jump);
    }

    [Fact]
    public void AutoJumpKeepsItsAttemptUntilItCanUseIt()
    {
        var controller = Ticking.Controller();
        controller.WalkTo(East(10), autoJump: true);

        controller.Tick(Ticking.At(Vector3d.Zero));

        // Bumping into something in mid-air cannot be answered with a jump, so
        // the one attempt is not spent on it.
        Assert.False(controller.Tick(Ticking.At(Vector3d.Zero, onGround: false, blocked: true)).Jump);
        Assert.True(controller.Tick(Ticking.At(Vector3d.Zero, blocked: true)).Jump);
    }

    [Fact]
    public async Task WithoutAutoJumpABlockedMoveEndsInsteadOfJumping()
    {
        var controller = Ticking.Controller();
        var movement = controller.WalkTo(East(10), autoJump: false);

        for (var tick = 0; tick < 40 && !movement.IsCompleted; tick++)
            Assert.False(controller.Tick(Ticking.At(Vector3d.Zero, blocked: true)).Jump);

        Assert.Equal(MovementResult.Blocked, await movement);
    }

    [Fact]
    public async Task AZeroDistanceMoveArrivesImmediately()
        => Assert.Equal(MovementResult.Arrived, await Ticking.Controller().WalkTo(Vector3d.Zero));

    [Fact]
    public void NoMovementMeansNoDirection()
        => Assert.Null(Ticking.Controller().Tick(Ticking.At(Vector3d.Zero)).Direction);

}
