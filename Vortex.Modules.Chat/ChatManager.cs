using Vortex.Modules.Chat.Abstraction;
using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Chat;

internal class ChatManager(INetworkingManager networking) : IChatManager
{
    public async Task SendMessage(string message)
    {
        await networking.SendPacket(new ChatMessage(message, DateTime.Now.Ticks, new Random().NextInt64(), null, 1, 0));
    }
}
