using System.Globalization;
using Vortex.Bot.Commands.Infrastructure;
using Vortex.Data;
using Vortex.Framework.Abstraction;
using Vortex.Modules.Entities.Abstraction;

namespace Vortex.Bot.Commands.World;

[Command("near", "Lists the five closest entities")]
public sealed class NearCommand : BotCommand
{
    public override Task ExecuteAsync(CommandContext context)
    {
        var client = context.Client;
        var nearby = client.Entities.Entities
            .OrderBy(entity => entity.Position.DistanceTo(client.Position))
            .Take(5)
            .Select(entity => string.Create(
                CultureInfo.InvariantCulture,
                $"{Describe(client, entity)} ({entity.Position.DistanceTo(client.Position):F1}m)"))
            .ToList();

        return context.ReplyAsync(nearby.Count == 0 ? "Nobody around" : string.Join(", ", nearby));
    }

    private static string Describe(IVortexClient client, Entity entity)
    {
        if (entity.Type == EntityType.Player && client.Entities.GetPlayer(entity.Uuid) is { } player)
            return player.Name;

        if (entity.Item is { } stack)
            return $"{stack.Count}x {stack.Item}";

        return entity.Health is { } health
            ? string.Create(CultureInfo.InvariantCulture, $"{entity.Type} {health:F0}hp")
            : entity.Type.ToString();
    }
}
