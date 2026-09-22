using Vortex.Bot.Commands.Infrastructure;

namespace Vortex.Bot.Commands.Items;

[Command("inv", "Tells what I hold and carry")]
public sealed class InventoryCommand : BotCommand
{
    public override Task ExecuteAsync(CommandContext context)
    {
        var inventory = context.Client.Inventory;

        var held = inventory.HeldItem is { } stack
            ? $"{stack.Item}" + (stack.MaxDamage > 0 ? $" ({stack.MaxDamage - stack.Damage}/{stack.MaxDamage})" : "")
            : "nothing";

        return context.ReplyAsync(
            $"Holding {held}. Carrying " + ItemListing.Describe(inventory.Find(_ => true).Select(found => found.Stack), "nothing"));
    }
}
