using Microsoft.Extensions.Logging;
using Vortex.Modules.Chat.Abstraction;
using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Chat;

internal class ChatPacketHandler(IChatManager manager, ILogger<ChatPacketHandler> logger) : IPacketHandler<SystemChatMessage>
{
    public Task HandleAsync(SystemChatMessage packet)
    {
        logger.LogInformation("Received system chat message: {Text}", packet.Text);

        return Task.CompletedTask;
    }
}
