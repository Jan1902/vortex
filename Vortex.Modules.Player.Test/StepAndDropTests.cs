using System.Globalization;
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
        var controller = Ticking.Controller();

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
        var controller = Ticking.Controller();

        var start = Ticking.At(new Vector3d(2.5, 64, 0.5));
        controller.Tick(start);

        var stepped = controller.StepUpTo(new Vector3d(Ledge + 0.5, 67, 0.5));

        Ticking.Walk(controller, world, start, stepped);

        Assert.Equal(MovementResult.Blocked, await stepped);
    }

    [Theory]
    [InlineData(1, MovementMode.Walk)]
    [InlineData(2, MovementMode.Walk)]
    [InlineData(3, MovementMode.Walk)]
    [InlineData(1, MovementMode.Sprint)]
    [InlineData(3, MovementMode.Sprint)]
    public async Task DropsOffALedgeOntoTheBlockBelowOrTheOneAfter(int height, MovementMode mode)
    {
        var floor = 64 - height;

        // From the middle of the last block, where a route starts a drop, from
        // on the lip itself, and from a spread of spots in between.
        var starts = Enumerable.Range(0, 20).Select(i => Ledge - 0.5 - i * 0.05)
            .Concat([Ledge - 0.1, Ledge + 0.1, Ledge + 0.25]);

        foreach (var x in starts)
        {
            var world = Lowered(by: height);
            var controller = Ticking.Controller();

            var start = Ticking.At(new Vector3d(x, 64, 0.5));
            controller.Tick(start);

            var dropped = controller.DropTo(new Vector3d(Ledge + 0.5, floor, 0.5), mode);
            var ended = Ticking.Walk(controller, world, start, dropped);

            Assert.Equal(MovementResult.Arrived, await dropped);
            Assert.Equal(floor, ended.Position.Y, precision: 3);

            // Letting go before the edge puts it on the block below; a fall
            // cannot be stopped once it is going, so the next one along is as
            // near as it can be promised.
            Assert.InRange(ended.Position.X, Ledge, Ledge + 2);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void DropsOntoASingleBlockWithNothingBeyondIt(int height)
    {
        var floor = 64 - height;

        // From a few blocks back, arriving at a walk, and from anywhere between
        // the middle of the last block and the lip.
        var starts = Enumerable.Range(0, 60).Select(i => Ledge - 3.5 + i * 0.055);
        var failures = new List<string>();

        foreach (var x in starts)
        {
            // One block to come down on, and past it a long way down.
            var world = new Terrain(column => column < Ledge ? 63 : column == Ledge ? 63 - height : 40);
            var controller = Ticking.Controller();

            var start = Ticking.At(new Vector3d(x, 64, 0.5));
            controller.Tick(start);

            var dropped = controller.DropTo(new Vector3d(Ledge + 0.5, floor, 0.5));
            var ended = Ticking.Walk(controller, world, start, dropped);

            // Past the block is a long way down, so landing on it is not enough:
            // it has to be on it, not hanging off the far side of it.
            if (dropped.Result != MovementResult.Arrived
                || Math.Abs(ended.Position.Y - floor) > 0.001
                || ended.Position.X is < Ledge or > Ledge + 1)
                failures.Add(string.Create(CultureInfo.InvariantCulture,
                    $"from {x:F2}: {dropped.Result} at {ended.Position.X:F2} {ended.Position.Y:F2}"));
        }

        Assert.Empty(failures);
    }

    [Fact]
    public async Task ADropThatComesDownTwoBlocksOutHasMissed()
    {
        var controller = Ticking.Controller();

        controller.Tick(Ticking.At(new Vector3d(Ledge - 0.5, 64, 0.5)));

        var dropped = controller.DropTo(new Vector3d(Ledge + 0.5, 63, 0.5));

        controller.Tick(Ticking.At(new Vector3d(Ledge + 1.5, 63.5, 0.5), onGround: false));
        controller.Tick(Ticking.At(new Vector3d(Ledge + 2.5, 63, 0.5)));

        Assert.Equal(MovementResult.Blocked, await dropped);
    }

    [Fact]
    public void ADropEndsOnTheGroundRatherThanInTheAir()
    {
        var world = Lowered(by: 3);
        var controller = Ticking.Controller();

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
