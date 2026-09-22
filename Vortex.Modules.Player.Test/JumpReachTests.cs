using Vortex.Modules.Navigation.Abstraction;
using Vortex.Modules.Player.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Player.Test;

/// <summary>
/// How far the player can actually jump, against how far the search is told it
/// can.
/// </summary>
/// <remarks>
/// <para>
/// The pathfinder plans jumps from a table it cannot check. These tests are what
/// holds the two together: every gap <see cref="JumpReach"/> promises is jumped
/// here, with the real controller and the real physics, and it has to come down
/// on the block it was aimed at. If a jump ever stops carrying that far, this
/// fails here rather than dropping the bot into a hole out there.
/// </para>
/// <para>
/// Each jump starts where a route starts it -- standing in the middle of the
/// last block -- and from twenty slightly different spots around there, so that
/// a promise cannot hold only because the ticks happened to fall well.
/// </para>
/// </remarks>
public class JumpReachTests
{
    /// <summary>Every kind of jump the search is allowed to plan.</summary>
    public static TheoryData<bool, int> Planned()
    {
        var data = new TheoryData<bool, int>();

        foreach (var sprinting in new[] { false, true })
            for (var rise = JumpReach.HighestLanding; rise >= JumpReach.LowestLanding; rise--)
                data.Add(sprinting, rise);

        return data;
    }

    [Theory]
    [MemberData(nameof(Planned))]
    public void ClearsEveryGapTheSearchPlans(bool sprinting, int rise)
    {
        var widest = JumpReach.WidestGap(sprinting, rise);

        // A sprint is only planned where walking falls short, so that is all it
        // has to be good for.
        var narrowest = sprinting ? JumpReach.WidestGap(sprinting: false, rise) + 1 : 1;

        for (var gap = narrowest; gap <= widest; gap++)
            Assert.True(
                FromEverywhere(gap, rise, sprinting),
                $"a {gap} block gap should be cleared {Describe(sprinting, rise)}");
    }

    [Theory]
    [MemberData(nameof(Planned))]
    public void DoesNotPromiseMoreThanItDoes(bool sprinting, int rise)
    {
        var widest = JumpReach.WidestGap(sprinting, rise);

        // If this ever starts failing, the jump got better and the table is
        // leaving reach on the table.
        Assert.False(
            FromEverywhere(widest + 1, rise, sprinting),
            $"a {widest + 1} block gap is promised to be out of reach {Describe(sprinting, rise)}");
    }

    [Fact]
    public void FallingBuysDistance()
    {
        // More time in the air is more ground covered, which is why the table is
        // not one number.
        Assert.True(JumpReach.WidestGap(sprinting: true, -2) > JumpReach.WidestGap(sprinting: true, 1));
    }

    private static string Describe(bool sprinting, int rise)
        => $"{(sprinting ? "sprinting" : "walking")}, landing {rise:+#;-#;level} block(s)";

    /// <summary>Whether a jump lands from each of twenty starting spots.</summary>
    private static bool FromEverywhere(int gap, int rise, bool sprinting)
        => Enumerable.Range(0, 20).All(i => Lands(gap, rise, sprinting, -0.5 - i * 0.05));

    /// <summary>
    /// Jumps east from a floor that stops at x = 0 across a gap of a given
    /// width, and says whether the player came down on the block it aimed at.
    /// </summary>
    private static bool Lands(int gap, int rise, bool sprinting, double startX)
    {
        var world = new Terrain(x => x < 0 ? 63 : x < gap ? null : 63 + rise);
        var controller = Ticking.Controller();

        var start = Ticking.At(new Vector3d(startX, 64, 0.5));
        controller.Tick(start);

        var leap = controller.JumpTo(
            new Vector3d(0, 64, 0.5),
            new Vector3d(gap + 0.5, 64 + rise, 0.5),
            sprinting ? MovementMode.Sprint : MovementMode.Walk);

        return Ticking.Flies(controller, world, start, leap);
    }
}
