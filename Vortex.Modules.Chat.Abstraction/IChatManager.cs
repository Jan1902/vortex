namespace Vortex.Modules.Chat.Abstraction;

/// <summary>
/// Represents a chat manager.
/// </summary>
public interface IChatManager
{
    /// <summary>
    /// Sends a chat message.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendMessage(string message);
}
