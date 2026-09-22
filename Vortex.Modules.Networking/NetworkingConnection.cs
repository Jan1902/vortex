using Microsoft.Extensions.Logging;
using System.Buffers;
using System.IO.Pipelines;
using System.Net.Sockets;
using Vortex.Framework.Abstraction;
using Vortex.Modules.Networking.Abstraction;
using Vortex.Modules.Networking.CustomTypes;
using Vortex.Modules.Networking.PacketHandling;
using Vortex.Modules.Networking.Packets;

namespace Vortex.Modules.Networking;

internal class NetworkingConnection(
    VortexClientConfiguration configuration,
    ILogger<NetworkingConnection> logger,
    NetworkingController packetManager,
    IEventBus eventBus,
    PacketSerializer packetSerializer)
{
    private readonly Socket _socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

    private int _compressionThreshold = PacketFraming.CompressionDisabled;

    /// <summary>
    /// The op code of the Set Compression packet, resolved from its type so that a
    /// change of the packet ID does not silently break the compression handshake.
    /// </summary>
    private readonly int? _setCompressionOpCode = packetSerializer.GetOpCode(typeof(SetCompressionPacket));

    /// <summary>
    /// Serializes writes to the socket so concurrent senders cannot interleave frames.
    /// </summary>
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    /// <summary>Set once the client closes the connection itself, which is then no error.</summary>
    private volatile bool _disconnecting;

    public async Task Connect()
    {
        if (configuration.Hostname == default
            || configuration.Port == default)
            throw new ArgumentException("Invalid connection information specified in configuration!");

        try
        {
            await _socket.ConnectAsync(configuration.Hostname, configuration.Port);
            logger.LogInformation("Connected to server at {host}: {port}", configuration.Hostname, configuration.Port);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error connecting to host {host} at port {port}", configuration.Hostname, configuration.Port);
            throw;
        }

        // Start receiving before announcing the connection, so the replies to
        // whatever the handlers send are never missed. The loop logs its own
        // failures and ends with the connection.
        _ = Task.Run(ReceiveLoop);

        await eventBus.PublishAsync(new ConnectionEstablishedEvent());
    }

    public async Task Disconnect()
    {
        if (_disconnecting || !_socket.Connected)
            return;

        _disconnecting = true;

        // Wait for a send in progress, so no frame is cut off half way.
        await _sendLock.WaitAsync();
        try
        {
            _socket.Shutdown(SocketShutdown.Both);
            _socket.Close();
        }
        finally
        {
            _sendLock.Release();
        }

        logger.LogInformation("Disconnected from the server");
    }

    public async Task SendPacket(PacketBase packet)
    {
        if (_disconnecting || !_socket.Connected)
            return;

        var opCode = packetSerializer.GetOpCode(packet.GetType());
        if (opCode is null)
            return;

        var data = packetSerializer.SerializePacket(packet.GetType(), packet);
        var frame = PacketFraming.BuildFrame(opCode.Value, data, _compressionThreshold);

        await _sendLock.WaitAsync();
        try
        {
            await _socket.SendAsync(frame, SocketFlags.None);

            logger.LogTrace("Sent packet of type {packetType}", packet.GetType().Name);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error sending packet with OP code {op} and size {size} to server", opCode, data.Length);
            throw;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// Reads from the socket and processes packets until the connection ends.
    /// The socket is drained by one task while another consumes complete frames,
    /// so a slow handler applies backpressure instead of dropping data.
    /// </summary>
    private async Task ReceiveLoop()
    {
        var pipe = new Pipe();

        await Task.WhenAll(FillPipe(pipe.Writer), ReadPipe(pipe.Reader));

        if (!_disconnecting)
            logger.LogWarning("Connection to server has been terminated");
    }

    private async Task FillPipe(PipeWriter writer)
    {
        const int minimumBufferSize = 16384;

        while (true)
        {
            var memory = writer.GetMemory(minimumBufferSize);

            int bytesRead;
            try
            {
                bytesRead = await _socket.ReceiveAsync(memory, SocketFlags.None);
            }
            catch (Exception e)
            {
                if (!_disconnecting)
                    logger.LogError(e, "Error receiving data from server");

                break;
            }

            if (bytesRead == 0)
                break;

            writer.Advance(bytesRead);

            var result = await writer.FlushAsync();
            if (result.IsCompleted)
                break;
        }

        await writer.CompleteAsync();
    }

    private async Task ReadPipe(PipeReader reader)
    {
        while (true)
        {
            var result = await reader.ReadAsync();
            var buffer = result.Buffer;

            try
            {
                // Packets are awaited one at a time on purpose: the protocol is
                // order dependent, and a state change has to take effect before the
                // next packet is unwrapped.
                while (PacketFraming.TryReadFrame(ref buffer, out var payload))
                    await ProcessPacket(payload);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error processing incoming data, closing connection");
                break;
            }

            reader.AdvanceTo(buffer.Start, buffer.End);

            if (result.IsCompleted)
                break;
        }

        await reader.CompleteAsync();
    }

    private async Task ProcessPacket(ReadOnlySequence<byte> payload)
    {
        if (!TryUnwrapPayload(payload, out var opCode, out var body))
        {
            logger.LogError("Received a malformed packet frame");
            return;
        }

        // Compression changes how every following frame is read, so it is applied
        // here rather than dispatched to a handler.
        if (packetManager.State == ProtocolState.Login && opCode == _setCompressionOpCode)
        {
            EnableCompression(body);
            return;
        }

        try
        {
            await packetManager.HandlePacket(opCode, body);
        }
        catch (Exception e)
        {
            // A packet the client mishandles should not take down the connection.
            logger.LogError(e, "Error handling packet with OP code 0x{op:X2}", opCode);
        }
    }

    /// <summary>
    /// Unwraps a frame payload. Kept separate from the asynchronous caller because
    /// a span cannot live across an await.
    /// </summary>
    private bool TryUnwrapPayload(ReadOnlySequence<byte> payload, out int opCode, out byte[] body)
        // A frame rarely spans segments, but when it does it has to be flattened first.
        => payload.IsSingleSegment
            ? PacketFraming.TryUnwrapPayload(payload.FirstSpan, _compressionThreshold, out opCode, out body)
            : PacketFraming.TryUnwrapPayload(payload.ToArray(), _compressionThreshold, out opCode, out body);

    private void EnableCompression(byte[] body)
    {
        if (!VarInt.TryReadVarInt(body, out var threshold, out _))
        {
            logger.LogError("Set Compression packet is missing its threshold");
            return;
        }

        _compressionThreshold = threshold;

        if (threshold < 0)
            logger.LogInformation("Server disabled packet compression");
        else
            logger.LogInformation("Enabled packet compression with a threshold of {threshold} bytes", threshold);
    }
}
