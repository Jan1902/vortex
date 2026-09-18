using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Modules.Player;
using Vortex.Modules.Player.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Player.Test;

public class MovementControllerTests
{
    private static readonly Vector3d _east = new(1, 0, 0);

    [Fact]
    public async Task ArrivesAfterCoveringTheDistance()
    {
        var controller = new MovementController(NullLogger<MovementController>.Instance);
        var movement = controller.Move(_east, distance: 3);

        var position = Vector3d.Zero;

        for (var tick = 0; tick < 40 && !movement.IsCompleted; tick++)
        {
            controller.GetInput(position);
            position = position with { X = position.X + 0.2 };
            controller.OnStepped(Step(position));
        }

        Assert.Equal(MovementResult.Arrived, await movement);
        Assert.False(controller.IsMoving);
    }

    [Fact]
    public async Task ReportsBlockedWhenItStopsMakingProgress()
    {
        var controller = new MovementController(NullLogger<MovementController>.Instance);
        var movement = controller.Move(_east, distance: 10);

        var position = Vector3d.Zero;

        // The player never moves, which is what running into a wall looks like.
        for (var tick = 0; tick < 40 && !movement.IsCompleted; tick++)
        {
            controller.GetInput(position);
            controller.OnStepped(Step(position, blocked: true));
        }

        Assert.Equal(MovementResult.Blocked, await movement);
    }

    [Fact]
    public async Task ReportsBlockedWhenItGrindsAlongWithoutProgress()
    {
        var controller = new MovementController(NullLogger<MovementController>.Instance);
        var movement = controller.Move(_east, distance: 10);

        var position = Vector3d.Zero;

        // Moving, but sideways: no collision is reported, yet the target never
        // gets closer. This is the case a pure collision check would miss.
        for (var tick = 0; tick < 60 && !movement.IsCompleted; tick++)
        {
            controller.GetInput(position);
            position = position with { Z = position.Z + 0.2 };
            controller.OnStepped(Step(position));
        }

        Assert.Equal(MovementResult.Blocked, await movement);
    }

    [Fact]
    public async Task StoppingCancelsTheMovement()
    {
        var controller = new MovementController(NullLogger<MovementController>.Instance);
        var movement = controller.Move(_east, distance: 10);

        controller.Stop();

        Assert.Equal(MovementResult.Cancelled, await movement);
        Assert.False(controller.IsMoving);
    }

    [Fact]
    public async Task ANewMovementCancelsThePreviousOne()
    {
        var controller = new MovementController(NullLogger<MovementController>.Instance);

        var first = controller.Move(_east, distance: 10);
        var second = controller.Move(new Vector3d(0, 0, 1), distance: 10);

        Assert.Equal(MovementResult.Cancelled, await first);
        Assert.False(second.IsCompleted);
    }

    [Fact]
    public void PassesTheModeAndDirectionThrough()
    {
        var controller = new MovementController(NullLogger<MovementController>.Instance);
        controller.Move(_east, distance: 5, MovementMode.Sprint);

        var input = controller.GetInput(Vector3d.Zero);

        Assert.NotNull(input.Direction);
        Assert.Equal(1, input.Direction!.X, precision: 6);
        Assert.Equal(MovementMode.Sprint, input.Mode);
    }

    [Fact]
    public void JumpIsRequestedOnceAndThenCleared()
    {
        var controller = new MovementController(NullLogger<MovementController>.Instance);
        controller.Jump();

        Assert.True(controller.GetInput(Vector3d.Zero).Jump);
        Assert.False(controller.GetInput(Vector3d.Zero).Jump);
    }

    [Fact]
    public void AutoJumpTriesOnceWhenBlocked()
    {
        var controller = new MovementController(NullLogger<MovementController>.Instance);
        controller.Move(_east, distance: 10, autoJump: true);

        controller.GetInput(Vector3d.Zero);
        controller.OnStepped(Step(Vector3d.Zero, blocked: true, onGround: true));

        Assert.True(controller.GetInput(Vector3d.Zero).Jump);

        // Only one attempt per obstacle, otherwise a wall turns into endless hopping.
        controller.OnStepped(Step(Vector3d.Zero, blocked: true, onGround: true));

        Assert.False(controller.GetInput(Vector3d.Zero).Jump);
    }

    [Fact]
    public async Task WithoutAutoJumpABlockedMoveEndsInsteadOfJumping()
    {
        var controller = new MovementController(NullLogger<MovementController>.Instance);
        var movement = controller.Move(_east, distance: 10, autoJump: false);

        for (var tick = 0; tick < 40 && !movement.IsCompleted; tick++)
        {
            Assert.False(controller.GetInput(Vector3d.Zero).Jump);
            controller.OnStepped(Step(Vector3d.Zero, blocked: true, onGround: true));
        }

        Assert.Equal(MovementResult.Blocked, await movement);
    }

    [Fact]
    public async Task AZeroDistanceMoveArrivesImmediately()
        => Assert.Equal(MovementResult.Arrived, await new MovementController(NullLogger<MovementController>.Instance)
            .Move(_east, distance: 0));

    [Fact]
    public void NoMovementMeansNoDirection()
        => Assert.Null(new MovementController(NullLogger<MovementController>.Instance)
            .GetInput(Vector3d.Zero).Direction);

    private static PhysicsStep Step(Vector3d position, bool blocked = false, bool onGround = true)
        => new(position, Vector3d.Zero, onGround, blocked);
}
