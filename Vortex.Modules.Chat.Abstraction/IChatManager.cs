namespace Vortex.Modules.Chat.Abstraction;

public interface IChatManager
{
    Task SendMessage(string message);
}