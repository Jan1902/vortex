using Vortex.Bot.Commands.Infrastructure;
using Vortex.Shared;

namespace Vortex.Bot.Commands.World;

[Command("block", "Tells what block is somewhere")]
public sealed class BlockCommand : BotCommand
{
    [Argument(0)]
    public Vector3i Block { get; set; } = null!;

    public override Task ExecuteAsync(CommandContext context)
        => context.ReplyAsync(context.Client.GetBlock(Block)?.ToString() ?? "Nothing");
}
