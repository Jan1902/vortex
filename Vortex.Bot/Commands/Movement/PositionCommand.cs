using System.Globalization;
using Vortex.Bot.Commands.Infrastructure;
using Vortex.Shared;

namespace Vortex.Bot.Commands.Movement;

[Command("pos", "Tells where I am")]
public sealed class PositionCommand : BotCommand
{
    public override Task ExecuteAsync(CommandContext context)
    {
        var position = context.Client.Position;
        var block = position.ToBlockPosition();

        return context.ReplyAsync(string.Create(
            CultureInfo.InvariantCulture,
            $"I'm on block {block.X} {block.Y} {block.Z}, exactly {position.X:F2} {position.Y:F2} {position.Z:F2}, ")
            + (context.Client.IsOnGround ? "standing on solid ground" : "falling"));
    }
}
