using System.Globalization;
using Vortex.Data;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Tasks.Navigation;
using Vortex.Modules.Entities.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Tasks.Items;

/// <summary>
/// Picks up the items lying around a place.
/// </summary>
/// <remarks>
/// Best effort on purpose: an item that cannot be got at, or that does not go
/// in, is skipped rather than failed over. The server only says where an item
/// is about once a second, so the bot walks to where it last saw it and waits
/// a moment.
/// </remarks>
public class CollectItemsTask(Vector3d center, double radius) : BotTask
{
    /// <summary>How long to stand by an item before giving up on it.</summary>
    private static readonly TimeSpan PickupWait = TimeSpan.FromSeconds(2);

    /// <summary>How many items to gather before calling it a round.</summary>
    private const int MaxItems = 32;

    /// <summary>How far below itself a falling item is followed to where it will land.</summary>
    private const int MaxFall = 8;

    public override string Description
        => string.Create(CultureInfo.InvariantCulture, $"collect the items within {radius:0.#} blocks of {center.X:F0} {center.Y:F0} {center.Z:F0}");

    public override bool IsDone(Bot bot)
        => NextItem(bot, new HashSet<int>()) is null;

    public override async Task<TaskResult> RunAsync(Bot bot)
    {
        var skipped = new HashSet<int>();

        for (var picked = 0; picked < MaxItems; picked++)
        {
            if (NextItem(bot, skipped) is not { } item)
                return TaskResult.Success();

            var walked = await bot.Run(new GoNearTask(LandingOf(bot, item.Position.ToBlockPosition())));

            if (walked.IsFailure)
            {
                skipped.Add(item.Id);
                continue;
            }

            var deadline = DateTime.UtcNow + PickupWait;

            while (bot.Entities.Get(item.Id) is not null && DateTime.UtcNow < deadline)
                await Task.Delay(50, bot.Cancellation);

            if (bot.Entities.Get(item.Id) is not null)
                skipped.Add(item.Id);
        }

        return TaskResult.Success();
    }

    /// <summary>The nearest item in the area the inventory has room for.</summary>
    private Entity? NextItem(Bot bot, IReadOnlySet<int> skipped)
        => bot.Entities.Entities
            .Where(entity => entity.Type == EntityType.Item
                && !skipped.Contains(entity.Id)
                && entity.Item is { } stack
                && entity.Position.DistanceTo(center) <= radius
                && bot.Inventory.SpaceFor(stack.Item) > 0)
            .MinBy(entity => entity.Position.DistanceTo(bot.Player.Position));

    /// <summary>Where an item comes to rest: the block above the first solid one below it.</summary>
    private static Vector3i LandingOf(Bot bot, Vector3i block)
    {
        for (var fallen = 0; fallen < MaxFall; fallen++)
        {
            var below = block with { Y = block.Y - 1 };

            if (bot.World.GetBlock(below) is not { } state || BlockCollision.IsSolid(state))
                return block;

            block = below;
        }

        return block;
    }
}
