using Microsoft.Extensions.Logging;
using Vortex.Framework.Abstraction;
using Vortex.Modules.Chat.Abstraction;
using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Chat;

internal class ChatPacketHandler(ILogger<ChatPacketHandler> logger, IEventBus eventBus)
    : IPacketHandler<SystemChatMessage>,
    IPacketHandler<PlayerChatMessage>,
    IPacketHandler<DisguisedChatMessage>
{
    public Task HandleAsync(SystemChatMessage packet)
        => PublishMessage(ChatComponent.ToPlainText(packet.Text));

    public Task HandleAsync(PlayerChatMessage packet)
        => PublishMessage(packet.Message, packet.Sender);

    public Task HandleAsync(DisguisedChatMessage packet)
        => PublishMessage(ChatComponent.ToPlainText(packet.Message));

    private async Task PublishMessage(string text, Guid? sender = null)
    {
        if (string.IsNullOrEmpty(text))
            return;

        logger.LogInformation("Received chat message with text '{Text}'", text);

        await eventBus.PublishAsync(new ChatMessageReceivedEvent(text, sender));
    }
}
