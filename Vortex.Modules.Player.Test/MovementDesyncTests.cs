using Vortex.Modules.Player.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Player.Test;

public class MovementDesyncTests
{
    /// <summary>A point that many blocks east of the origin, where these start.</summary>
    private static Vector3d East(double blocks)
        => new(blocks, 0, 0);

    [Fact]
    public async Task ServerCorrectionEndsTheMovementAsDesynced()
    {
        var controller = Ticking.Controller();

        var move = controller.WalkTo(East(5));
        controller.Tick(Ticking.At(Vector3d.Zero));

        controller.Desynchronize();

        // Not Cancelled: the caller is meant to work out where to go again, not
        // to treat this as someone calling it off.
        Assert.Equal(MovementResult.Desynced, await move);
    }

    [Fact]
    public async Task DeliberateStopIsStillReportedAsCancelled()
    {
        var controller = Ticking.Controller();

        var move = controller.WalkTo(East(5));
        controller.Tick(Ticking.At(Vector3d.Zero));

        controller.Stop();

        Assert.Equal(MovementResult.Cancelled, await move);
    }

    [Fact]
    public void CorrectionWithNothingInProgressIsHarmless()
    {
        var controller = Ticking.Controller();

        controller.Desynchronize();

        Assert.False(controller.IsMoving);
    }

    [Fact]
    public async Task MovementDoesNotContinueAfterACorrection()
    {
        var controller = Ticking.Controller();

        var move = controller.WalkTo(East(5));
        controller.Tick(Ticking.At(Vector3d.Zero));

        controller.Desynchronize();
        await move;

        // The target was worked out from a position that no longer holds, so
        // nothing should still be walking towards it.
        Assert.False(controller.IsMoving);
        Assert.Equal(MovementInput.Idle, controller.Tick(Ticking.At(Vector3d.Zero)));
    }
}
