namespace Vortex.Framework.Abstraction;

/// <summary>
/// A chat message arrived.
/// </summary>
/// <param name="SenderUuid">The player who wrote it, or <c>null</c> for a message from the server.</param>
/// <param name="SenderName">The player's name, if the tab list knows it.</param>
public record ChatMessageReceivedEventArgs(string Message, Guid? SenderUuid = null, string? SenderName = null);