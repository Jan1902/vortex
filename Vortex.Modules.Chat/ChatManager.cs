using Vortex.Modules.Chat.Abstraction;
using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Chat;

internal class ChatManager(INetworkingConnection connection) : IChatManager
{
    public async Task SendMessage(string message)
    {
        await connection.SendPacket(new ChatMessage(message, DateTime.Now.Ticks, new Random().NextInt64(), null, 1));
    }
}
