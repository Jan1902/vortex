using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using Vortex.Data;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Tasks.Helper;
using Vortex.Modules.Interaction.Abstraction;
using Vortex.Modules.Inventory.Abstraction;
using Vortex.Modules.Player.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Tasks.Navigation;

/// <summary>
/// Carries out the moves of a route that put a block down: pillaring up and
/// bridging across.
/// </summary>
/// <remarks>
/// <para>
/// Both place against a block the player is standing on or has just stood on,
/// so the block to place against is always there and always in reach. What
/// they need from the inventory is a block that nobody will miss.
/// </para>
/// <para>
/// The world is not told about a block placed until the server says so, which
/// takes as long as the connection does. Pillaring is the one that cares: the
/// player falls back onto where the block goes a few ticks after placing it, so
/// a very slow connection can leave it landing before the block is there.
/// </para>
/// </remarks>
internal static class Building
{
    /// <summary>
    /// Blocks the bot may use up to build its way somewhere: common, cheap,
    /// solid, and staying where they are put.
    /// </summary>
    public static FrozenSet<Item> Throwaway { get; } = new[]
    {
        Item.Dirt, Item.CoarseDirt,
        Item.Cobblestone, Item.CobbledDeepslate,
        Item.Andesite, Item.Diorite, Item.Granite, Item.Tuff,
        Item.Netherrack,
    }.ToFrozenSet();

    /// <summary>How long to wait for the jump of a pillar to lift the feet clear, in ticks.</summary>
    private const int RiseTicks = 12;

    /// <summary>How long to wait for the server to show a placed block, in ticks.</summary>
    private const int PlaceTicks = 20;

    /// <summary>How far past the middle of its block the player leans out to bridge, sneaking so as not to fall.</summary>
    private const double BridgeLean = 0.8;

    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(50);

    /// <summary>How many throwaway blocks the bot carries.</summary>
    public static int Count(Bot bot)
        => bot.Inventory.Find(stack => Throwaway.Contains(stack.Item)).Sum(found => found.Stack.Count);

    /// <summary>
    /// Goes up a block by jumping and placing one where the feet were.
    /// </summary>
    /// <param name="from">The block the player is standing in.</param>
    /// <returns>Whether the player ended up standing on the new block.</returns>
    public static async Task<bool> PillarAsync(Bot bot, Vector3i from)
    {
        var floor = from + new Vector3i(0, -1, 0);

        if (!await HoldThrowawayAsync(bot))
            return false;

        // Over the middle, so the jump comes straight down onto the block.
        await bot.Movement.WalkTo(new Vector3d(from.X + 0.5, from.Y, from.Z + 0.5));

        bot.Player.LookAt(BlockFaces.Center(floor, BlockFace.Up));
        await Task.Delay(Tick, bot.Cancellation);

        bot.Movement.Jump();

        // Place as soon as the feet are clear of where the block goes: the
        // sooner it is down, the longer the server has to say so before the
        // player comes back down onto it.
        for (var waited = 0; waited < RiseTicks * 10 && bot.Player.Position.Y < from.Y + 1; waited++)
            await Task.Delay(Tick / 10, bot.Cancellation);

        if (bot.Player.Position.Y < from.Y + 1)
        {
            bot.Logger.LogDebug("The jump to pillar from {X} {Y} {Z} never got high enough", from.X, from.Y, from.Z);

            return false;
        }

        if (!await bot.Interaction.UseItemOnBlockAsync(floor, BlockFace.Up, cursor: new Vector3f(0.5f, 1f, 0.5f), cancellationToken: bot.Cancellation))
            return false;

        if (!await AppearsAsync(bot, from))
            return false;

        // Back on the ground, on top of it.
        for (var waited = 0; waited < PlaceTicks && !bot.Player.IsOnGround; waited++)
            await Task.Delay(Tick, bot.Cancellation);

        return bot.Player.Position.ToBlockPosition().Y == from.Y + 1;
    }

    /// <summary>
    /// Steps across a gap onto a block placed in it, against the side of the
    /// block the player stands on.
    /// </summary>
    /// <param name="from">The block the player is standing in.</param>
    /// <param name="to">The block across the gap to end up in.</param>
    /// <returns>Whether the player ended up there.</returns>
    public static async Task<bool> BridgeAsync(Bot bot, Vector3i from, Vector3i to)
    {
        var floor = from + new Vector3i(0, -1, 0);
        var support = to + new Vector3i(0, -1, 0);

        if (!await HoldThrowawayAsync(bot))
            return false;

        // Out to the edge, sneaking, which will not step off it: the block goes
        // against the side of this one, and that has to be within easy reach.
        var direction = to - from;
        var edge = new Vector3d(from.X + 0.5 + direction.X * BridgeLean, from.Y, from.Z + 0.5 + direction.Z * BridgeLean);

        if (await bot.Movement.WalkTo(edge, MovementMode.Sneak) == MovementResult.Cancelled)
            return false;

        var face = BlockFaces.Facing(floor, new Vector3d(support.X + 0.5, support.Y + 0.5, support.Z + 0.5));
        var offset = face.Offset();

        bot.Player.LookAt(BlockFaces.Center(floor, face));
        await Task.Delay(Tick, bot.Cancellation);

        var cursor = new Vector3f(0.5f + offset.X * 0.5f, 0.5f, 0.5f + offset.Z * 0.5f);

        if (!await bot.Interaction.UseItemOnBlockAsync(floor, face, cursor: cursor, cancellationToken: bot.Cancellation))
            return false;

        if (!await AppearsAsync(bot, support))
            return false;

        var walked = await bot.Movement.WalkTo(new Vector3d(to.X + 0.5, to.Y, to.Z + 0.5));

        return walked == MovementResult.Arrived;
    }

    /// <summary>Gets a throwaway block into the main hand, preferring one already on the hotbar.</summary>
    private static async Task<bool> HoldThrowawayAsync(Bot bot)
    {
        var found = bot.Inventory.Find(stack => Throwaway.Contains(stack.Item))
            .OrderBy(candidate => candidate.Slot is >= PlayerSlots.HotbarStart and < PlayerSlots.HotbarStart + PlayerSlots.HotbarCount ? 0 : 1)
            .FirstOrDefault();

        if (found.Stack is null)
        {
            bot.Logger.LogDebug("No blocks left to build with");

            return false;
        }

        return await Hold.InMainHandAsync(bot.Inventory, found.Slot);
    }

    /// <summary>Waits for the server to show a block where one was placed.</summary>
    private static async Task<bool> AppearsAsync(Bot bot, Vector3i position)
    {
        for (var waited = 0; waited < PlaceTicks; waited++)
        {
            if (BlockCollision.IsSolid(bot.World.GetBlock(position)))
                return true;

            await Task.Delay(Tick, bot.Cancellation);
        }

        bot.Logger.LogDebug("The server never showed the block placed at {X} {Y} {Z}", position.X, position.Y, position.Z);

        return false;
    }
}
