using Vortex.Bot.Commands.Infrastructure;

namespace Vortex.Bot.Commands.Items;

[Command("close", "Closes the open container")]
public sealed class CloseCommand : BotCommand
{
    public override async Task ExecuteAsync(CommandContext context)
    {
        await context.Client.Inventory.CloseContainerAsync();

        await context.ReplyAsync("Closed");
    }
}
