using Vortex.Modules.Player;
using Vortex.Modules.Player.Abstraction;
using Vortex.Modules.World.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Player.Test;

public class PlayerPhysicsTests
{
    /// <summary>Y level of the floor's top surface in the test world.</summary>
    private const int FloorTop = 64;

    private static readonly Vector3d _east = new(1, 0, 0);

    [Fact]
    public void FallsWhenInTheAir()
    {
        var step = Step(FlatWorld(), new Vector3d(0.5, 100, 0.5));

        Assert.True(step.Position.Y < 100);
        Assert.False(step.OnGround);
        Assert.Equal(-PlayerPhysics.Gravity * PlayerPhysics.VerticalDrag, step.Velocity.Y, precision: 6);
    }

    [Fact]
    public void AcceleratesWhileFalling()
    {
        var physics = new PlayerPhysics(FlatWorld());

        var first = physics.Step(new Vector3d(0.5, 100, 0.5), Vector3d.Zero, false, MovementInput.Idle);
        var second = physics.Step(first.Position, first.Velocity, false, MovementInput.Idle);

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
            var step = physics.Step(position, velocity, false, MovementInput.Idle);
            position = step.Position;
            velocity = step.Velocity;

            Assert.True(velocity.Y >= -PlayerPhysics.TerminalVelocity);
        }

        Assert.InRange(velocity.Y, -PlayerPhysics.TerminalVelocity, -3.8);
    }

    [Fact]
    public void LandsOnTheFloorAndStaysThere()
    {
        var (position, velocity, _) = Simulate(FlatWorld(), new Vector3d(0.5, FloorTop + 20, 0.5), MovementInput.Idle, ticks: 100);

        Assert.Equal(FloorTop, position.Y, precision: 6);
        Assert.Equal(0, velocity.Y, precision: 6);
    }

    [Fact]
    public void ReportsStandingOnGround()
    {
        var step = Step(FlatWorld(), new Vector3d(0.5, FloorTop, 0.5));

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
            new Vector3d(0, -PlayerPhysics.TerminalVelocity, 0),
            false,
            MovementInput.Idle);

        Assert.Equal(FloorTop, step.Position.Y, precision: 6);
        Assert.True(step.OnGround);
    }

    [Fact]
    public void TreatsUnloadedChunksAsSolid()
    {
        // An empty world stands in for chunks that have not arrived yet. Falling
        // through the world while it loads would be worse than standing still.
        var step = Step(new FakeWorld(), new Vector3d(0.5, 100, 0.5));

        Assert.Equal(100, step.Position.Y, precision: 6);
        Assert.True(step.OnGround);
    }

    [Fact]
    public void WalksInTheGivenDirection()
    {
        var step = Step(FlatWorld(), Ground(0.5, 0.5), onGround: true, Walking());

        Assert.Equal(0.5 + PlayerPhysics.SpeedOf(MovementMode.Walk), step.Position.X, precision: 6);
        Assert.Equal(FloorTop, step.Position.Y, precision: 6);
    }

    [Fact]
    public void SprintsFasterThanItWalksAndSneaksSlower()
    {
        var world = FlatWorld();

        var walk = Step(world, Ground(0.5, 0.5), onGround: true, Walking());
        var sprint = Step(world, Ground(0.5, 0.5), onGround: true, Walking(MovementMode.Sprint));
        var sneak = Step(world, Ground(0.5, 0.5), onGround: true, Walking(MovementMode.Sneak));

        Assert.True(sprint.Position.X > walk.Position.X);
        Assert.True(sneak.Position.X < walk.Position.X);
    }

    [Fact]
    public void NormalizesTheDirectionSoDiagonalsAreNotFaster()
    {
        var world = FlatWorld();
        var start = Ground(0.5, 0.5);

        var straight = Step(world, start, onGround: true, new MovementInput(new Vector3d(5, 0, 0)));
        var diagonal = Step(world, start, onGround: true, new MovementInput(new Vector3d(1, 0, 1)));

        var straightDistance = straight.Position.HorizontalDistanceTo(start);
        var diagonalDistance = diagonal.Position.HorizontalDistanceTo(start);

        Assert.Equal(straightDistance, diagonalDistance, precision: 6);
    }

    [Fact]
    public void CoastsToAStopWithoutInput()
    {
        var physics = new PlayerPhysics(FlatWorld());

        var step = physics.Step(Ground(0.5, 0.5), new Vector3d(0.2, 0, 0), true, MovementInput.Idle);

        // On the ground both air resistance and block friction apply.
        Assert.Equal(0.2 * PlayerPhysics.HorizontalDrag * PlayerPhysics.GroundFriction, step.Velocity.X, precision: 6);
    }

    [Fact]
    public void SlowsDownFasterOnTheGroundThanInTheAir()
    {
        var physics = new PlayerPhysics(FlatWorld());
        var velocity = new Vector3d(0.2, 0, 0);

        var grounded = physics.Step(Ground(0.5, 0.5), velocity, true, MovementInput.Idle);
        var airborne = physics.Step(new Vector3d(0.5, FloorTop + 5, 0.5), velocity, false, MovementInput.Idle);

        Assert.True(grounded.Velocity.X < airborne.Velocity.X);
    }

    [Fact]
    public void ReportsBeingBlockedByAWall()
    {
        var world = FlatWorld();
        Ledge(world, x: 1);

        var step = Step(world, Ground(0.5, 0.5), onGround: true, Walking());

        Assert.Equal(0.5, step.Position.X, precision: 6);
        Assert.True(step.Blocked);
        Assert.Equal(0, step.Velocity.X, precision: 6);
    }

    [Fact]
    public void PassesThroughGrass()
    {
        var world = FlatWorld();
        world.SetBlock(new Vector3i(1, FloorTop, 0), "minecraft:short_grass");

        var step = Step(world, Ground(0.5, 0.5), onGround: true, Walking());

        Assert.False(step.Blocked);
        Assert.True(step.Position.X > 0.5);
    }

    [Fact]
    public void JumpsFromTheGround()
    {
        var step = Step(FlatWorld(), Ground(0.5, 0.5), onGround: true, new MovementInput(null, Jump: true));

        Assert.Equal(PlayerPhysics.JumpVelocity, step.Velocity.Y, precision: 6);
        Assert.True(step.Position.Y > FloorTop);
    }

    [Fact]
    public void DoesNotJumpWhileInTheAir()
    {
        var step = Step(FlatWorld(), new Vector3d(0.5, FloorTop + 5, 0.5), onGround: false, new MovementInput(null, Jump: true));

        Assert.True(step.Velocity.Y < 0);
    }

    [Fact]
    public void JumpsHighEnoughToClearOneBlock()
    {
        var physics = new PlayerPhysics(FlatWorld());

        var position = Ground(0.5, 0.5);
        var velocity = Vector3d.Zero;
        var highest = position.Y;
        var input = new MovementInput(null, Jump: true);

        for (var tick = 0; tick < 30; tick++)
        {
            var step = physics.Step(position, velocity, tick == 0, input);
            position = step.Position;
            velocity = step.Velocity;
            highest = Math.Max(highest, position.Y);

            input = MovementInput.Idle;
        }

        // A vanilla jump clears 1.25 blocks, which is what makes a full block passable.
        Assert.InRange(highest - FloorTop, 1.2, 1.3);
        Assert.Equal(FloorTop, position.Y, precision: 6);
    }

    [Fact]
    public void StepsOntoAnObstacleWithinStepHeight()
    {
        var world = FlatWorld();
        Ledge(world, x: 1);

        // Standing half a block high, the wall's top is within reach of a step.
        var physics = new PlayerPhysics(world);
        var step = physics.Step(new Vector3d(0.5, FloorTop + 0.5, 0.5), Vector3d.Zero, true, Walking());

        Assert.False(step.Blocked);
        Assert.Equal(FloorTop + 1, step.Position.Y, precision: 6);
        Assert.True(step.Position.X > 0.5);
    }

    [Fact]
    public void DoesNotStepOntoAFullBlock()
    {
        // A full block is 1.0 high and the step height is 0.6, so walking into one
        // is blocked. Getting up there needs a jump.
        var world = FlatWorld();
        Ledge(world, x: 1);

        var step = Step(world, Ground(0.5, 0.5), onGround: true, Walking());

        Assert.True(step.Blocked);
        Assert.Equal(FloorTop, step.Position.Y, precision: 6);
    }

    [Fact]
    public void DoesNotStepUpWhileAirborne()
    {
        var world = FlatWorld();
        Ledge(world, x: 1);

        var physics = new PlayerPhysics(world);
        var step = physics.Step(new Vector3d(0.5, FloorTop + 0.5, 0.5), Vector3d.Zero, false, Walking());

        Assert.True(step.Blocked);
    }

    [Fact]
    public void SneakingRefusesToWalkOffALedge()
    {
        var world = Platform();

        // Ground runs out at x = 2. With a 0.6 wide hitbox the player is still
        // supported at 2.25, and one sneaking step would take it past the edge.
        var start = new Vector3d(2.25, FloorTop, 0.5);
        var step = Step(world, start, onGround: true, Walking(MovementMode.Sneak));

        Assert.Equal(2.25, step.Position.X, precision: 6);
    }

    [Fact]
    public void WalkingOffALedgeIsAllowed()
    {
        var world = Platform();

        var start = new Vector3d(2.25, FloorTop, 0.5);
        var step = Step(world, start, onGround: true, Walking());

        Assert.True(step.Position.X > 2.25);
    }

    private static MovementInput Walking(MovementMode mode = MovementMode.Walk)
        => new(_east, mode);

    private static Vector3d Ground(double x, double z)
        => new(x, FloorTop, z);

    private static PhysicsStep Step(IWorldManager world, Vector3d position, bool onGround = false, MovementInput? input = null)
        => new PlayerPhysics(world).Step(position, Vector3d.Zero, onGround, input ?? MovementInput.Idle);

    private static (Vector3d Position, Vector3d Velocity, bool OnGround) Simulate(
        IWorldManager world, Vector3d start, MovementInput input, int ticks)
    {
        var physics = new PlayerPhysics(world);
        var position = start;
        var velocity = Vector3d.Zero;
        var onGround = false;

        for (var tick = 0; tick < ticks; tick++)
        {
            var step = physics.Step(position, velocity, onGround, input);
            position = step.Position;
            velocity = step.Velocity;
            onGround = step.OnGround;
        }

        return (position, velocity, onGround);
    }

    /// <summary>A one block high step at the given X, spanning the test area.</summary>
    private static void Ledge(FakeWorld world, int x)
    {
        for (var z = -8; z <= 8; z++)
            world.SetBlock(new Vector3i(x, FloorTop, z), "minecraft:stone");
    }

    /// <summary>A world with a stone floor whose top surface is at <see cref="FloorTop"/>.</summary>
    private static FakeWorld FlatWorld()
    {
        var world = new FakeWorld();

        for (var x = -8; x <= 8; x++)
            for (var z = -8; z <= 8; z++)
                world.SetBlock(new Vector3i(x, FloorTop - 1, z), "minecraft:stone");

        return world;
    }

    /// <summary>A floor that ends at x = 2, so there is an edge to fall off.</summary>
    private static FakeWorld Platform()
    {
        var world = new FakeWorld();

        for (var x = -8; x <= 1; x++)
            for (var z = -8; z <= 8; z++)
                world.SetBlock(new Vector3i(x, FloorTop - 1, z), "minecraft:stone");

        // Air above the drop, so the columns count as loaded rather than unknown.
        for (var x = 2; x <= 8; x++)
            for (var z = -8; z <= 8; z++)
                world.SetBlock(new Vector3i(x, FloorTop - 1, z), "minecraft:air");

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

            return _loadedChunks.Contains(new Vector2i(position.X >> 4, position.Z >> 4))
                ? new BlockState(0, "minecraft:air")
                : null;
        }

        public Chunk? GetChunk(Vector2i position) => null;
    }
}
