using Microsoft.Extensions.Logging;
using Vortex.Modules.Chat.Abstraction;
using Vortex.Modules.Networking.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Chat;

internal class ChatPacketHandler(IChatManager manager, ILogger<ChatPacketHandler> logger)
    : IPacketHandler<SystemChatMessage>,
    IPacketHandler<PlayerChatMessage>,
    IPacketHandler<DisguisedChatMessage>
{
    public Task HandleAsync(SystemChatMessage packet)
    {
        logger.LogInformation("Received system chat message: {Text}", packet.Text);

        return Task.CompletedTask;
    }

    public Task HandleAsync(PlayerChatMessage packet)
    {
        logger.LogInformation("Received chat message with text '{Text}'", packet.Message);

        return Task.CompletedTask;
    }

    public Task HandleAsync(DisguisedChatMessage packet)
    {
        logger.LogInformation("Received chat message with text '{Text}'", packet.Message is StringTag str ? str.Value : "Not implemented");

        return Task.CompletedTask;
    }
}
