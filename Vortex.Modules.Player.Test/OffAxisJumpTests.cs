using System.Globalization;
using Vortex.Data;
using Vortex.Modules.Navigation.Abstraction;
using Vortex.Modules.Player.Abstraction;
using Vortex.Modules.World.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Player.Test;

/// <summary>
/// Jumps that go neither along an axis nor straight across a corner, such as
/// two blocks on and one to the side, from one lone block to another.
/// </summary>
/// <remarks>
/// The search plans these as far as <see cref="JumpReach"/> says the player can
/// jump, measured centre to centre. These tests hold it to that for the shapes
/// a jump and run is made of, taking off the way a route does: from the edge of
/// the block, in the direction of the landing.
/// </remarks>
public class OffAxisJumpTests
{
    public static TheoryData<int, int, int> Planned()
    {
        var data = new TheoryData<int, int, int>();

        // Every landing the search can plan within reach of a sprint, which is
        // the furthest reach there is.
        for (var rise = JumpReach.HighestLanding; rise >= JumpReach.LowestLanding; rise--)
            for (var dx = 1; dx <= 4; dx++)
                for (var dz = 1; dz < dx; dz++)
                    if (Math.Sqrt(dx * dx + dz * dz) <= JumpReach.Furthest(sprinting: true, rise))
                        data.Add(dx, dz, rise);

        return data;
    }

    [Theory]
    [MemberData(nameof(Planned))]
    public void LandsEveryOffAxisJumpTheSearchPlans(int dx, int dz, int rise)
    {
        var distance = Math.Sqrt(dx * dx + dz * dz);
        var sprinting = distance > JumpReach.Furthest(sprinting: false, rise);

        var direction = new Vector3d(dx / distance, 0, dz / distance);
        var failures = new List<string>();

        // From the middle of the block, where a route starts a jump, and from
        // spots further back along the way it goes.
        foreach (var back in Enumerable.Range(0, 20).Select(i => i * 0.01))
        {
            var world = new Pillars((0, 0, 63), (dx, dz, 63 + rise));
            var controller = Ticking.Controller();

            var start = Ticking.At(new Vector3d(0.5 - direction.X * back, 64, 0.5 - direction.Z * back));
            controller.Tick(start);

            var leap = controller.JumpTo(
                new Vector3d(0.5 + direction.X * 0.5, 64, 0.5 + direction.Z * 0.5),
                new Vector3d(dx + 0.5, 64 + rise, dz + 0.5),
                sprinting ? MovementMode.Sprint : MovementMode.Walk);

            var ended = Ticking.Walk(controller, world, start, leap);

            if (leap.Result != MovementResult.Arrived)
                failures.Add(string.Create(CultureInfo.InvariantCulture,
                    $"{back:F2} back: {leap.Result} at {ended.Position.X:F2} {ended.Position.Y:F2} {ended.Position.Z:F2}"));
        }

        Assert.Empty(failures);
    }

    /// <summary>Single blocks standing in the void.</summary>
    private sealed class Pillars(params (int X, int Z, int Top)[] pillars) : IWorldManager
    {
        public BlockState? GetBlock(Vector3i position)
            => pillars.Any(pillar => pillar.X == position.X && pillar.Z == position.Z && position.Y == pillar.Top)
                ? BlockState.Default(Block.Stone)
                : BlockState.Default(Block.Air);

        public Chunk? GetChunk(Vector2i position)
            => throw new NotSupportedException("The physics reads single blocks.");
    }
}
