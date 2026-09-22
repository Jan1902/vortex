namespace Vortex.Modules.Chat.Abstraction;

/// <summary>
/// A chat message arrived.
/// </summary>
/// <param name="Sender">
/// The UUID of the player who wrote it, or <c>null</c> for a message from the
/// server, such as a death message or one sent by a command.
/// </param>
public record ChatMessageReceivedEvent(string Message, Guid? Sender = null);