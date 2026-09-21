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
/// The pathfinder plans jumps it can never try, from a table it cannot check.
/// These tests are what holds the two together: every gap
/// <see cref="JumpReach"/> promises is jumped here, with the real controller
/// deciding when to leave the ground and the real physics deciding where that
/// puts the player. If the physics ever stops carrying that far, this fails
/// here rather than dropping the bot into a hole out there.
/// </para>
/// <para>
/// The other direction is checked too. A table that undersells the physics
/// costs the bot routes it could have walked, so each promise is also the last
/// one that works.
/// </para>
/// </remarks>
public class JumpReachTests
{
    /// <summary>Every jump the search is allowed to plan.</summary>
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

        Assert.True(widest > 0, $"nothing planned for {Describe(sprinting, rise)}");

        for (var gap = 1; gap <= widest; gap++)
            Assert.True(
                Clears(gap, rise, sprinting),
                $"a {gap} block gap should be clearable {Describe(sprinting, rise)}");
    }

    [Theory]
    [MemberData(nameof(Planned))]
    public void DoesNotPretendToClearWhatItCannot(bool sprinting, int rise)
    {
        var widest = JumpReach.WidestGap(sprinting, rise);

        // If this ever starts failing, the physics got better and the table is
        // leaving reach on the table.
        Assert.False(
            Clears(widest + 1, rise, sprinting),
            $"a {widest + 1} block gap is promised to be out of reach {Describe(sprinting, rise)}");
    }

    [Fact]
    public void FallingBuysDistance()
    {
        // More time in the air is more ground covered, which is why the table
        // is not one number. A jump down carries further than one across, and
        // one across further than a jump up.
        Assert.True(JumpReach.WidestGap(sprinting: true, -2) > JumpReach.WidestGap(sprinting: true, 1));
    }

    private static string Describe(bool sprinting, int rise)
        => $"{(sprinting ? "sprinting" : "walking")}, landing {rise:+#;-#;level} block(s)";

    /// <summary>
    /// Runs east along a floor that stops at x = 0, leaps a gap of a given
    /// width, and says whether the player ended up on the far side.
    /// </summary>
    /// <remarks>
    /// The whole movement, not just the arc: where to take off from is the
    /// controller's decision and a real part of how far a jump reaches.
    /// </remarks>
    private static bool Clears(int gap, int rise, bool sprinting)
    {
        var world = new Terrain(x => x < 0 ? 63 : x < gap ? null : 63 + rise);
        var controller = Ticking.Controller(world);

        var start = Ticking.At(new Vector3d(-6.5, 64, 0.5));
        controller.Tick(start);

        var leap = controller.JumpTo(
            new Vector3d(0, 64, 0.5),
            new Vector3d(gap + 0.5, 64 + rise, 0.5),
            sprinting ? MovementMode.Sprint : MovementMode.Walk);

        return Ticking.Flies(controller, world, start, leap);
    }
}
