using Vortex.Bot.Commands.Infrastructure;
using Vortex.Modules.Behaviour.Tasks;
using Vortex.Shared;

namespace Vortex.Bot.Commands.Items;

[Command("chest", "Opens a container and tells what is in it")]
public sealed class ChestCommand : BotCommand
{
    [Argument(0)]
    public Vector3i Block { get; set; } = null!;

    public override async Task ExecuteAsync(CommandContext context)
    {
        var client = context.Client;
        var result = await client.Brain.RunAsync(client.Brain.CreateTask<OpenContainerTask>(Block));

        if (result.IsFailure || client.Inventory.OpenContainer is not { } container)
        {
            await context.ReplyAsync(result.ToString());
            return;
        }

        await context.ReplyAsync($"{container.Type}: " + ItemListing.Describe(container.Slots.Take(container.ContainerSize), "empty"));
    }
}
