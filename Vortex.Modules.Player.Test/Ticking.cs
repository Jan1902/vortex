using Microsoft.Extensions.Logging.Abstractions;
using Vortex.Data;
using Vortex.Modules.Player.Abstraction;
using Vortex.Modules.World.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Player.Test;

/// <summary>
/// Drives a controller by hand, one tick at a time.
/// </summary>
/// <remarks>
/// The controller reads the state a tick starts from and answers with what to
/// hold down, so a test that wants to put the player somewhere says so by
/// handing over the state rather than by reporting a step afterwards.
/// </remarks>
internal static class Ticking
{
    /// <summary>The floor these tests stand on.</summary>
    public const int FloorTop = 64;

    /// <summary>Standing still on solid ground at a position.</summary>
    public static MovementState At(Vector3d position, bool onGround = true, bool blocked = false)
        => new(position, Vector3d.Zero, onGround, blocked);

    /// <summary>Moving through a position at a velocity.</summary>
    public static MovementState At(Vector3d position, Vector3d velocity, bool onGround)
        => new(position, velocity, onGround, Blocked: false);

    /// <summary>A controller the way the bot wires it.</summary>
    public static MovementController Controller()
        => new(NullLogger<MovementController>.Instance);

    /// <summary>
    /// Runs a movement to its end, driving the real physics with the real
    /// controller the way the bot's loop does.
    /// </summary>
    /// <param name="controller">The controller the movement was asked of.</param>
    /// <param name="world">The world to walk through.</param>
    /// <param name="start">Where the player begins.</param>
    /// <param name="movement">The movement in progress.</param>
    /// <returns>Where the player ended up.</returns>
    public static MovementState Walk(
        MovementController controller,
        IWorldManager world,
        MovementState start,
        Task<MovementResult> movement)
    {
        var physics = new PlayerPhysics(world);
        var state = start;

        for (var tick = 0; tick < 400 && !movement.IsCompleted; tick++)
        {
            var step = physics.Step(
                state.Position, state.Velocity, state.OnGround, controller.Tick(state));

            state = new MovementState(step.Position, step.Velocity, step.OnGround, step.Blocked, step.InWater);
        }

        Assert.True(movement.IsCompleted, "the movement never finished");

        return state;
    }

    /// <summary>
    /// Runs a movement to its end and says whether it worked.
    /// </summary>
    /// <remarks>
    /// For the ones that are allowed to fail. A jump that falls short drops into
    /// the gap, which in a world with no floor under it means falling until the
    /// plan gives up -- an answer, not a hang.
    /// </remarks>
    public static bool Flies(
        MovementController controller,
        IWorldManager world,
        MovementState start,
        Task<MovementResult> movement)
        => Walk(controller, world, start, movement) is not null
        && movement.Result == MovementResult.Arrived;
}

/// <summary>
/// Solid ground up to a given height in each column, and air above it.
/// </summary>
/// <remarks>
/// Enough to build a ledge, a drop or a staircase out of, which is what the
/// movements that are not plain walking need to be tried against.
/// </remarks>
/// <param name="surface">
/// The topmost solid block in a column, or null where the column is empty all
/// the way down.
/// </param>
internal sealed class Terrain(Func<int, int?> surface) : IWorldManager
{
    public BlockState? GetBlock(Vector3i position)
        => surface(position.X) is { } top && position.Y <= top
            ? BlockState.Default(Block.Stone)
            : BlockState.Default(Block.Air);

    public Chunk? GetChunk(Vector2i position)
        => throw new NotSupportedException("The physics reads single blocks.");
}

/// <summary>
/// Endless floor at <see cref="Ticking.FloorTop"/>, with a run of it missing.
/// </summary>
/// <remarks>An empty range -- a <c>gapTo</c> below <c>gapFrom</c> -- is flat ground.</remarks>
internal sealed class Gapped(int gapFrom, int gapTo) : IWorldManager
{
    public BlockState? GetBlock(Vector3i position)
    {
        if (position.Y != Ticking.FloorTop - 1)
            return BlockState.Default(Block.Air);

        var inGap = position.X >= gapFrom && position.X <= gapTo;

        return inGap
            ? BlockState.Default(Block.Air)
            : BlockState.Default(Block.Stone);
    }

    public Chunk? GetChunk(Vector2i position)
        => throw new NotSupportedException("The physics reads single blocks.");
}
