using Vortex.Modules.Player.Abstraction;
using Vortex.Modules.World.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Player.Test;

/// <summary>
/// Jumping onto a block one higher, which is what the pathfinder asks for at
/// every step up.
/// </summary>
public class StepUpJumpTests
{
    private const int FloorTop = 64;

    /// <summary>The raised block sits here, with its top at FloorTop + 1.</summary>
    private const int LedgeX = 3;

    private static readonly Vector3d _east = new(1, 0, 0);

    [Fact]
    public void LandsOnTheBlockItJumpedAt()
    {
        var landing = JumpOntoLedge();

        // The ledge is the single block at x = 3, so its top surface spans
        // x 3.0 to 4.0. Landing anywhere else means it sailed over.
        Assert.Equal(FloorTop + 1, landing.Y, precision: 3);
        Assert.InRange(landing.X, LedgeX, LedgeX + 1);
    }

    [Fact]
    public void DoesNotCarryOnPastTheBlock()
    {
        var landing = JumpOntoLedge();

        // What a player does: press into the block, jump, come down on top of
        // it. Full walking speed in mid-air would carry it clean over.
        Assert.True(
            landing.X < LedgeX + 1,
            $"jumped over the block and landed at x = {landing.X:F2}");
    }

    [Fact]
    public void JumpingWhilePressedAgainstABlockGoesUpRatherThanForward()
    {
        var world = LedgeWorld();
        var physics = new PlayerPhysics(world);

        // Walk east until the ledge stops the player dead.
        var position = new Vector3d(0.5, FloorTop, 0.5);
        var velocity = Vector3d.Zero;

        while (true)
        {
            var walked = physics.Step(position, velocity, onGround: true, new MovementInput(_east));

            position = walked.Position;
            velocity = walked.Velocity;

            if (walked.Blocked)
                break;
        }

        // The jump is taken from a standstill, because the block took all the
        // speed away. Holding forward through the flight must not give it back.
        var takeOff = position.X;
        var onGround = true;
        var jumped = physics.Step(position, velocity, onGround, new MovementInput(_east) { Jump = true });

        position = jumped.Position;
        velocity = jumped.Velocity;
        onGround = jumped.OnGround;

        Assert.Equal(takeOff, position.X, precision: 6);

        for (var tick = 0; tick < 40 && !onGround; tick++)
        {
            var step = physics.Step(position, velocity, onGround, new MovementInput(_east));

            position = step.Position;
            velocity = step.Velocity;
            onGround = step.OnGround;
        }

        // Enough to clear the block's face and settle on top of it, nowhere near
        // enough to cross it.
        Assert.InRange(position.X - takeOff, 0.3, 1.0);
    }

    [Fact]
    public void ARunningJumpStillCarriesTheMomentumItHad()
    {
        var world = FlatWorld();
        var physics = new PlayerPhysics(world);

        var start = new Vector3d(0.5, FloorTop, 0.5);
        var position = start;
        var velocity = Vector3d.Zero;
        var onGround = true;

        // Build up speed first, then jump and keep pressing forward.
        for (var tick = 0; tick < 10; tick++)
        {
            var step = physics.Step(position, velocity, onGround, new MovementInput(_east));
            position = step.Position;
            velocity = step.Velocity;
            onGround = step.OnGround;
        }

        var takeOff = position.X;
        var jumped = physics.Step(position, velocity, onGround, new MovementInput(_east) { Jump = true });

        position = jumped.Position;
        velocity = jumped.Velocity;
        onGround = jumped.OnGround;

        for (var tick = 0; tick < 40 && !onGround; tick++)
        {
            var step = physics.Step(position, velocity, onGround, new MovementInput(_east));
            position = step.Position;
            velocity = step.Velocity;
            onGround = step.OnGround;
        }

        // A jump taken at speed has to keep covering ground, or the bot could
        // never clear a gap.
        Assert.InRange(position.X - takeOff, 1.5, 4.0);
    }

    /// <summary>
    /// Walks east into a block one higher, jumping at it the way the movement
    /// controller does, and reports where the player came down.
    /// </summary>
    private static Vector3d JumpOntoLedge()
    {
        var world = LedgeWorld();
        var physics = new PlayerPhysics(world);

        var position = new Vector3d(0.5, FloorTop, 0.5);
        var velocity = Vector3d.Zero;
        var onGround = true;

        var jumped = false;

        for (var tick = 0; tick < 200; tick++)
        {
            // The controller gives an obstacle one jump, once it is actually
            // pressed against it.
            var wantsJump = !jumped && onGround && tick > 0 && Math.Abs(velocity.X) < 1e-9 && position.X > 1;

            if (wantsJump)
                jumped = true;

            var step = physics.Step(position, velocity, onGround, new MovementInput(_east) { Jump = wantsJump });

            position = step.Position;
            velocity = step.Velocity;
            onGround = step.OnGround;

            if (jumped && onGround && position.Y > FloorTop)
                return position;

            if (jumped && onGround && tick > 40)
                return position;
        }

        return position;
    }

    private static FakeWorld FlatWorld()
    {
        var world = new FakeWorld();

        for (var x = -8; x <= 16; x++)
            for (var z = -8; z <= 8; z++)
                world.SetBlock(new Vector3i(x, FloorTop - 1, z), "minecraft:stone");

        return world;
    }

    /// <summary>Flat ground with a single block raised at <see cref="LedgeX"/>.</summary>
    private static FakeWorld LedgeWorld()
    {
        var world = FlatWorld();

        for (var z = -8; z <= 8; z++)
            world.SetBlock(new Vector3i(LedgeX, FloorTop, z), "minecraft:stone");

        return world;
    }

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

        public Chunk? GetChunk(Vector2i position)
            => throw new NotSupportedException();
    }
}
