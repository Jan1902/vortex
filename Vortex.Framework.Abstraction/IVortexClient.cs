﻿using Vortex.Shared;

namespace Vortex.Framework.Abstraction;

/// <summary>
/// Represents a Vortex client.
/// </summary>
public interface IVortexClient
{
    /// <summary>
    /// Starts the Vortex client asynchronously.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StartAsync();

    /// <summary>
    /// Sends a chat message.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendChatMessage(string message);

    /// <summary>
    /// Gets the block state at the specified position.
    /// </summary>
    /// <param name="position">The position of the block.</param>
    /// <returns>The block state at the specified position.</returns>
    BlockState? GetBlock(Vector3i position);
}
