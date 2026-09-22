using Vortex.Modules.Interaction.Abstraction;
using Vortex.Modules.Player.Abstraction;
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
    /// Looks at the middle of the side of a block that faces the bot.
    /// </summary>
    /// <returns>That side, which the act should name.</returns>
    public static async Task<BlockFace> AtBlockAsync(IPlayerManager player, Vector3i block, CancellationToken cancellationToken)
    {
        var eyes = player.Position + new Vector3d(0, EyeHeight, 0);
        var face = BlockFaces.Facing(block, eyes);

        player.LookAt(BlockFaces.Center(block, face));
        await Task.Delay(Tick, cancellationToken);

        return face;
    }
}
