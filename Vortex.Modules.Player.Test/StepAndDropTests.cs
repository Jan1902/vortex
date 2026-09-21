using Vortex.Modules.Player.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Player.Test;

/// <summary>
/// Getting onto a block one higher, and off a ledge onto one lower, with the
/// real controller driving the real physics.
/// </summary>
/// <remarks>
/// Both used to be plain walks with a flag on them: a step up was a walk that
/// was allowed to jump at whatever it bumped into, and a drop was a walk
/// followed by waiting for the falling to stop. They are movements in their own
/// right now, which is what lets them say whether they worked.
/// </remarks>
public class StepAndDropTests
{
    /// <summary>The ground changes level at this column.</summary>
    private const int Ledge = 6;

    [Fact]
    public async Task StepsUpOntoABlockOneHigher()
    {
        var world = Raised(by: 1);
        var controller = Ticking.Controller(world);

        var start = Ticking.At(new Vector3d(2.5, 64, 0.5));
        controller.Tick(start);

        var target = new Vector3d(Ledge + 0.5, 65, 0.5);
        var stepped = controller.StepUpTo(target);

        var ended = Ticking.Walk(controller, world, start, stepped);

        Assert.Equal(MovementResult.Arrived, await stepped);
        Assert.Equal(65, ended.Position.Y, precision: 3);
        Assert.InRange(ended.Position.X, Ledge, Ledge + 1);
    }

    [Fact]
    public async Task ReportsAStepUpItCouldNotMake()
    {
        // Three blocks is a wall, not a step. Walking into it and hoping is what
        // the old flag did; saying so is what a movement of its own can do.
        var world = Raised(by: 3);
        var controller = Ticking.Controller(world);

        var start = Ticking.At(new Vector3d(2.5, 64, 0.5));
        controller.Tick(start);

        var stepped = controller.StepUpTo(new Vector3d(Ledge + 0.5, 67, 0.5));

        Ticking.Walk(controller, world, start, stepped);

        Assert.Equal(MovementResult.Blocked, await stepped);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task DropsOffALedgeOntoTheBlockBelow(int height)
    {
        var world = Lowered(by: height);
        var controller = Ticking.Controller(world);

        var start = Ticking.At(new Vector3d(2.5, 64, 0.5));
        controller.Tick(start);

        var floor = 64 - height;
        var dropped = controller.DropTo(new Vector3d(Ledge + 0.5, floor, 0.5));

        var ended = Ticking.Walk(controller, world, start, dropped);

        Assert.Equal(MovementResult.Arrived, await dropped);
        Assert.Equal(floor, ended.Position.Y, precision: 3);

        // The fall is taken straight down rather than steered through: pushing
        // in mid-air would only carry the player past the block it was aimed at.
        Assert.InRange(ended.Position.X, Ledge, Ledge + 1);
    }

    [Fact]
    public void ADropEndsOnTheGroundRatherThanInTheAir()
    {
        var world = Lowered(by: 3);
        var controller = Ticking.Controller(world);

        var start = Ticking.At(new Vector3d(2.5, 64, 0.5));
        controller.Tick(start);

        var dropped = controller.DropTo(new Vector3d(Ledge + 0.5, 61, 0.5));

        // Whatever comes next is decided from where the player is standing, so a
        // drop that reported back mid-fall would be asking about a block it is
        // not on and is not staying at.
        Assert.True(Ticking.Walk(controller, world, start, dropped).OnGround);
    }

    /// <summary>Flat ground that steps up by a number of blocks at the ledge.</summary>
    private static Terrain Raised(int by)
        => new(x => x < Ledge ? 63 : 63 + by);

    /// <summary>Flat ground that falls away by a number of blocks at the ledge.</summary>
    private static Terrain Lowered(int by)
        => new(x => x < Ledge ? 63 : 63 - by);
}
