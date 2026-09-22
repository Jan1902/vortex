using Microsoft.Extensions.Logging;
using Vortex.Data;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Tasks.Helper;
using Vortex.Modules.Behaviour.Tasks.Navigation;
using Vortex.Modules.Inventory.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Behaviour.Tasks.Entities;

/// <summary>Fights an entity until it is dead, walking after it between hits.</summary>
public class AttackTask(int entityId) : BotTask
{
    /// <summary>How close the bot has to be to hit, from the eyes to the middle of the entity.</summary>
    private const double Reach = 3.0;

    /// <summary>How many hits it may take before something is clearly wrong.</summary>
    private const int MaxHits = 100;

    public override string Description
        => $"fight entity {entityId}";

    public override bool IsDone(Bot bot)
        => bot.Entities.Get(entityId) is null or { Health: <= 0 };

    public override async Task<TaskResult> RunAsync(Bot bot)
    {
        for (var hit = 0; hit < MaxHits; hit++)
        {
            if (IsDone(bot))
                return TaskResult.Success();

            if (bot.Entities.Get(entityId) is not { } entity)
                return TaskResult.Success();

            if (!InReach(bot, entity.Center))
            {
                var walked = await bot.Run(new GoNearTask(entity.Position.ToBlockPosition()));

                if (walked.IsFailure)
                    return walked;

                continue;
            }

            var before = bot.Inventory.HeldItem?.Item;

            if (BestWeapon(bot) is { } weapon)
                await Hold.InMainHandAsync(bot.Inventory, weapon);

            var held = bot.Inventory.HeldItem?.Item;

            // Swapping weapons resets the swing; hitting at once does no damage.
            if (held != before)
                await Task.Delay(Cooldown(held), bot.Cancellation);

            bot.Player.LookAt(entity.Center);
            await Task.Delay(50, bot.Cancellation);

            bot.Logger.LogDebug("Hitting {Type} {EntityId} with {Weapon}", entity.Type, entityId, held?.ToString() ?? "the bare hand");

            await bot.Interaction.AttackAsync(entityId);
            await Task.Delay(Cooldown(held), bot.Cancellation);
        }

        return TaskResult.Failed($"entity {entityId} is still alive after {MaxHits} hits");
    }

    private static TimeSpan Cooldown(Item? weapon)
        => TimeSpan.FromSeconds(1 / (weapon ?? Item.Air).AttackSpeed());

    private static bool InReach(Bot bot, Vector3d target)
        => (bot.Player.Position + new Vector3d(0, Aim.EyeHeight, 0)).DistanceTo(target) <= Reach;

    private static int? BestWeapon(Bot bot)
        => bot.Inventory.Find(stack => stack.Item.AttackDamage() > 1)
            .OrderByDescending(found => found.Stack.Item.AttackDamage())
            .ThenBy(found => found.Slot == PlayerSlots.Hotbar(bot.Inventory.SelectedHotbarSlot) ? 0 : 1)
            .Select(found => (int?)found.Slot)
            .FirstOrDefault();
}
