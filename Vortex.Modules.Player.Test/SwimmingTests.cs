using Vortex.Data;
using Vortex.Modules.Player.Abstraction;
using Vortex.Modules.World.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Player.Test;

/// <summary>
/// Water: floating in it, swimming across it, getting out of it, and falling
/// into it, with the real controller driving the real physics.
/// </summary>
public class SwimmingTests
{
    /// <summary>The top layer of water is this block; its surface is a little below the top of it.</summary>
    private const int Surface = 63;

    [Fact]
    public void FloatsAtTheSurfaceHoldingJump()
    {
        var physics = new PlayerPhysics(new Lake());
        var position = new Vector3d(2.5, Surface - 1, 0.5);
        var velocity = Vector3d.Zero;
        var onGround = false;

        for (var tick = 0; tick < 200; tick++)
        {
            var step = physics.Step(position, velocity, onGround, MovementInput.Idle with { Jump = true });

            (position, velocity, onGround) = (step.Position, step.Velocity, step.OnGround);
        }

        // Bobbing at the top, with the eyes clear of the water.
        Assert.InRange(position.Y, Surface, Surface + 1.5);
        Assert.True(position.Y + 1.62 > Surface + PlayerPhysics.WaterSurface, $"eyes under water at {position.Y:F2}");
    }

    [Fact]
    public void SinksWithoutSwimming()
    {
        var physics = new PlayerPhysics(new Lake());
        var position = new Vector3d(2.5, Surface, 0.5);
        var velocity = Vector3d.Zero;
        var onGround = false;

        for (var tick = 0; tick < 200; tick++)
        {
            var step = physics.Step(position, velocity, onGround, MovementInput.Idle);

            (position, velocity, onGround) = (step.Position, step.Velocity, step.OnGround);
        }

        Assert.True(onGround, "never reached the bottom");
        Assert.Equal(Lake.Bottom + 1, position.Y, precision: 3);
    }

    [Fact]
    public async Task SwimsAlongTheSurface()
    {
        var controller = Ticking.Controller();
        var start = Ticking.At(new Vector3d(1.5, Surface + 0.5, 0.5), onGround: false) with { InWater = true };

        controller.Tick(start);

        var swum = controller.SwimTo(new Vector3d(4.5, Surface, 0.5));
        var ended = Ticking.Walk(controller, new Lake(), start, swum);

        Assert.Equal(MovementResult.Arrived, await swum);
        Assert.InRange(ended.Position.X, 4, 5);
        Assert.True(ended.Position.Y + 1.62 > Surface + PlayerPhysics.WaterSurface, $"eyes under water at {ended.Position.Y:F2}");
    }

    [Fact]
    public async Task WalksThroughShallowWaterWithoutFloatingOff()
    {
        // Water a block deep over the floor: the head is above it standing, so
        // this is walking, and walking ends standing on the bottom. Swimming up
        // off the bottom on the way leaves the player bobbing at the top,
        // never landing, and never done.
        var world = new Lake(depth: 1);
        var controller = Ticking.Controller();
        var start = Ticking.At(new Vector3d(1.5, Lake.Floor(1) + 1, 0.5)) with { InWater = true };

        controller.Tick(start);

        var walked = controller.WalkTo(new Vector3d(4.5, Lake.Floor(1) + 1, 0.5));
        var ticks = 0;
        var state = start;
        var physics = new PlayerPhysics(world);

        for (; ticks < 400 && !walked.IsCompleted; ticks++)
        {
            var step = physics.Step(state.Position, state.Velocity, state.OnGround, controller.Tick(state));

            state = new MovementState(step.Position, step.Velocity, step.OnGround, step.Blocked, step.InWater);
        }

        Assert.Equal(MovementResult.Arrived, await walked);
        Assert.True(ticks < 60, $"took {ticks} ticks to wade three blocks");
    }

    [Fact]
    public async Task ClimbsOutOntoABankAboveTheWater()
    {
        // The bank at x = 6 has its top a block above the top layer of water.
        var controller = Ticking.Controller();
        var start = Ticking.At(new Vector3d(4.5, Surface + 0.5, 0.5), onGround: false) with { InWater = true };

        controller.Tick(start);

        var climbed = controller.StepUpTo(new Vector3d(Lake.Bank + 0.5, Surface + 1, 0.5));
        var ended = Ticking.Walk(controller, new Lake(), start, climbed);

        Assert.Equal(MovementResult.Arrived, await climbed);
        Assert.Equal(Surface + 1, ended.Position.Y, precision: 3);
    }

    [Fact]
    public async Task WadesIntoTheWaterFromTheBank()
    {
        var controller = Ticking.Controller();
        var start = Ticking.At(new Vector3d(Lake.Bank + 1.5, Surface + 1, 0.5));

        controller.Tick(start);

        // Off the bank and in: a drop of one onto the water.
        var dropped = controller.DropTo(new Vector3d(Lake.Bank - 0.5, Surface, 0.5));

        Ticking.Walk(controller, new Lake(), start, dropped);

        Assert.Equal(MovementResult.Arrived, await dropped);
    }

    [Fact]
    public async Task FallsIntoWaterFromHighUp()
    {
        var world = new Lake(cliff: 20);
        var controller = Ticking.Controller();
        var start = Ticking.At(new Vector3d(-0.5, Surface + 21, 0.5));

        controller.Tick(start);

        var dropped = controller.DropTo(new Vector3d(1.5, Surface, 0.5));

        Ticking.Walk(controller, world, start, dropped);

        Assert.Equal(MovementResult.Arrived, await dropped);
    }

    /// <summary>
    /// A lake three deep from x = 0 to 5, with a bank at x = 6 and beyond whose
    /// top is a block above the top layer of water. Optionally a cliff at
    /// x = -1 and below, rising some blocks above the bank.
    /// </summary>
    private sealed class Lake(int cliff = 0, int depth = 3) : IWorldManager
    {
        public const int Bottom = Surface - 3;
        public const int Bank = 6;

        /// <summary>The top of the solid bottom of a lake this deep.</summary>
        public static int Floor(int depth)
            => Surface - depth;

        public BlockState? GetBlock(Vector3i position)
        {
            if (position.X < 0)
                return Block(position.Y <= Surface + cliff);

            if (position.X >= Bank)
                return Block(position.Y <= Surface);

            if (position.Y <= Surface - depth)
                return Block(solid: true);

            return BlockState.Default(position.Y <= Surface ? Data.Block.Water : Data.Block.Air);
        }

        public Chunk? GetChunk(Vector2i position)
            => throw new NotSupportedException("The physics reads single blocks.");

        private static BlockState Block(bool solid)
            => BlockState.Default(solid ? Data.Block.Stone : Data.Block.Air);
    }
}
