using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Interaction.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Tasks.Helper;

/// <summary>
/// Turns the bot towards a block before acting on it.
/// </summary>
internal static class Aim
{
    /// <summary>Where the player's eyes sit above its feet.</summary>
    public const double EyeHeight = 1.62;

    /// <summary>
    /// A tick, so the new look direction goes out with the next movement packet
    /// before the act does.
    /// </summary>
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Looks at a part of a block the bot can see.
    /// </summary>
    /// <returns>
    /// The side looked at, which the act should name, or null if no part of
    /// the block is in sight and nothing should be done to it from here.
    /// </returns>
    public static async Task<BlockFace?> AtBlockAsync(Bot bot, Vector3i block)
    {
        if (Sight(bot, block) is not { } sight)
            return null;

        bot.Player.LookAt(sight.Point);
        await Task.Delay(Tick, bot.Cancellation);

        return Face(sight.Side);
    }

    /// <summary>
    /// A point on a block the bot can see from where it stands, and the side
    /// it is on, or null if none of it is in sight.
    /// </summary>
    /// <remarks>
    /// Anything solid is in the way, as it is for the pathfinder choosing where
    /// to stand, so that the two agree on what can be reached.
    /// </remarks>
    public static (Vector3d Point, Vector3i Side)? Sight(Bot bot, Vector3i block)
        => LineOfSight.Sight(
            LineOfSight.Eyes(bot.Player.Position),
            block,
            position => BlockCollision.IsSolid(bot.World.GetBlock(position)));

    private static BlockFace Face(Vector3i side)
        => Enum.GetValues<BlockFace>().First(face => face.Offset() == side);
}
