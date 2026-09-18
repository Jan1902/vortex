using Vortex.Modules.Player;
using Vortex.Modules.World.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Player.Test;

public class PlayerPhysicsTests
{
    /// <summary>Y level of the floor's top surface in the test world.</summary>
    private const int FloorTop = 64;

    [Fact]
    public void FallsWhenInTheAir()
    {
        var physics = new PlayerPhysics(FlatWorld());

        var step = physics.Step(new Vector3d(0.5, 100, 0.5), Vector3d.Zero);

        Assert.True(step.Position.Y < 100);
        Assert.False(step.OnGround);
        Assert.Equal(-PlayerPhysics.Gravity * PlayerPhysics.VerticalDrag, step.Velocity.Y, precision: 6);
    }

    [Fact]
    public void AcceleratesWhileFalling()
    {
        var physics = new PlayerPhysics(FlatWorld());

        var first = physics.Step(new Vector3d(0.5, 100, 0.5), Vector3d.Zero);
        var second = physics.Step(first.Position, first.Velocity);

        Assert.True(second.Velocity.Y < first.Velocity.Y);
    }

    [Fact]
    public void ApproachesTerminalVelocityWithoutExceedingIt()
    {
        var physics = new PlayerPhysics(FlatWorld());

        var position = new Vector3d(0.5, 100000, 0.5);
        var velocity = Vector3d.Zero;

        for (var tick = 0; tick < 200; tick++)
        {
            var step = physics.Step(position, velocity);
            position = step.Position;
            velocity = step.Velocity;

            // The drag formula converges on terminal velocity from above and must
            // never overshoot it.
            Assert.True(velocity.Y >= -PlayerPhysics.TerminalVelocity);
        }

        // Close to the limit, but never exactly on it: the approach is asymptotic.
        Assert.InRange(velocity.Y, -PlayerPhysics.TerminalVelocity, -3.8);
    }

    [Fact]
    public void LandsOnTheFloorAndStaysThere()
    {
        var physics = new PlayerPhysics(FlatWorld());

        var position = new Vector3d(0.5, FloorTop + 20, 0.5);
        var velocity = Vector3d.Zero;

        // Falling twenty blocks takes well under a hundred ticks.
        for (var tick = 0; tick < 100; tick++)
        {
            var step = physics.Step(position, velocity);
            position = step.Position;
            velocity = step.Velocity;
        }

        Assert.Equal(FloorTop, position.Y, precision: 6);
        Assert.Equal(0, velocity.Y, precision: 6);
    }

    [Fact]
    public void ReportsStandingOnGround()
    {
        var physics = new PlayerPhysics(FlatWorld());

        var step = physics.Step(new Vector3d(0.5, FloorTop, 0.5), Vector3d.Zero);

        Assert.True(step.OnGround);
        Assert.Equal(FloorTop, step.Position.Y, precision: 6);
    }

    [Fact]
    public void DoesNotFallThroughTheFloorAtHighSpeed()
    {
        var physics = new PlayerPhysics(FlatWorld());

        // Terminal velocity is almost four blocks per tick, so a single tick can
        // cross the floor entirely if the landing is resolved naively.
        var step = physics.Step(
            new Vector3d(0.5, FloorTop + 2, 0.5),
            new Vector3d(0, -PlayerPhysics.TerminalVelocity, 0));

        Assert.Equal(FloorTop, step.Position.Y, precision: 6);
        Assert.True(step.OnGround);
    }

    [Fact]
    public void TreatsUnloadedChunksAsSolid()
    {
        // An empty world stands in for chunks that have not arrived yet. Falling
        // through the world while it loads would be worse than standing still.
        var physics = new PlayerPhysics(new FakeWorld());

        var step = physics.Step(new Vector3d(0.5, 100, 0.5), Vector3d.Zero);

        Assert.Equal(100, step.Position.Y, precision: 6);
        Assert.True(step.OnGround);
    }

    [Fact]
    public void WalksAcrossFlatGround()
    {
        var physics = new PlayerPhysics(FlatWorld());

        var step = physics.Step(new Vector3d(0.5, FloorTop, 0.5), new Vector3d(0.2, 0, 0));

        Assert.Equal(0.7, step.Position.X, precision: 6);
        Assert.Equal(FloorTop, step.Position.Y, precision: 6);
    }

    [Fact]
    public void IsBlockedByAWall()
    {
        var world = FlatWorld();
        // A wall directly to the east of the spawn column.
        world.SetBlock(new Vector3i(1, FloorTop, 0), "minecraft:stone");
        world.SetBlock(new Vector3i(1, FloorTop + 1, 0), "minecraft:stone");

        var physics = new PlayerPhysics(world);

        var step = physics.Step(new Vector3d(0.5, FloorTop, 0.5), new Vector3d(0.3, 0, 0));

        Assert.Equal(0.5, step.Position.X, precision: 6);
        Assert.Equal(0, step.Velocity.X, precision: 6);
    }

    [Fact]
    public void PassesThroughGrass()
    {
        var world = FlatWorld();
        world.SetBlock(new Vector3i(1, FloorTop, 0), "minecraft:short_grass");

        var physics = new PlayerPhysics(world);

        var step = physics.Step(new Vector3d(0.5, FloorTop, 0.5), new Vector3d(0.3, 0, 0));

        Assert.Equal(0.8, step.Position.X, precision: 6);
    }

    /// <summary>
    /// A world with a stone floor whose top surface is at <see cref="FloorTop"/>.
    /// </summary>
    private static FakeWorld FlatWorld()
    {
        var world = new FakeWorld();

        for (var x = -8; x <= 8; x++)
            for (var z = -8; z <= 8; z++)
                world.SetBlock(new Vector3i(x, FloorTop - 1, z), "minecraft:stone");

        return world;
    }

    /// <summary>
    /// A world holding individually placed blocks. Everything not placed is air,
    /// except that an entirely unknown position returns null, which stands for a
    /// chunk that has not been received.
    /// </summary>
    private sealed class FakeWorld : IWorldManager
    {
        private readonly Dictionary<Vector3i, BlockState> _blocks = [];
        private readonly HashSet<Vector2i> _loadedChunks = [];

        public void SetBlock(Vector3i position, string blockName)
        {
            _blocks[position] = new BlockState(0, blockName);
            _loadedChunks.Add(new Vector2i(position.X >> 4, position.Z >> 4));
        }

        public BlockState? GetBlock(Vector3i position)
        {
            if (_blocks.TryGetValue(position, out var block))
                return block;

            // Inside a loaded chunk, an absent block is air.
            return _loadedChunks.Contains(new Vector2i(position.X >> 4, position.Z >> 4))
                ? new BlockState(0, "minecraft:air")
                : null;
        }

        public Chunk? GetChunk(Vector2i position) => null;
    }
}
