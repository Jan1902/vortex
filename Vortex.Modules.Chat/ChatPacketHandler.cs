using Microsoft.Extensions.Logging;
using Vortex.Framework.Abstraction;
using Vortex.Modules.Chat.Abstraction;
using Vortex.Modules.Networking.Abstraction;
using Vortex.Shared;

namespace Vortex.Modules.Chat;

internal class ChatPacketHandler(ILogger<ChatPacketHandler> logger, IEventBus eventBus)
    : IPacketHandler<SystemChatMessage>,
    IPacketHandler<PlayerChatMessage>,
    IPacketHandler<DisguisedChatMessage>
{
    public async Task HandleAsync(SystemChatMessage packet)
    {
        logger.LogInformation("Received system chat message: {Text}", packet.Text);

        await eventBus.PublishAsync(new ChatMessageReceivedEvent(packet.Text));
    }

    public async Task HandleAsync(PlayerChatMessage packet)
    {
        logger.LogInformation("Received chat message with text '{Text}'", packet.Message);

        await eventBus.PublishAsync(new ChatMessageReceivedEvent(packet.Message));
    }

    public async Task HandleAsync(DisguisedChatMessage packet)
    {
        var text = packet.Message is StringTag str ? str.Value : "Not implemented";
        logger.LogInformation("Received chat message with text '{Text}'", text);

        if (text is null)
            return;

        await eventBus.PublishAsync(new ChatMessageReceivedEvent(text));
    }
}
