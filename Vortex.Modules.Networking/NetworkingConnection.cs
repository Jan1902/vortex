using Microsoft.Extensions.Logging;
using Microsoft.IO;
using System.Net.Sockets;
using Vortex.Framework;
using Vortex.Framework.Abstraction;
using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Networking;

internal class NetworkingConnection(
    VortexClientConfiguration configuration,
    ILogger<NetworkingConnection> logger,
    NetworkingController packetManager,
    IEventBus eventBus,
    PacketSerializer packetSerializer)
{
    private readonly RecyclableMemoryStreamManager _streamManager = new();

    private readonly Socket _socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

    private int _currentQueuePosition;
    private readonly byte[] _buffer = new byte[16384];
    private readonly byte[] _dataQueue = new byte[262144];

    private bool _compressionEnabled;

    public async Task Connect()
    {
        if (configuration.Hostname == default
            || configuration.Port == default)
            throw new ArgumentException("Invalid connection information specified in configuration!");

        try
        {
            await _socket.ConnectAsync(configuration.Hostname, configuration.Port);
            logger.LogInformation("Connected to server at {host}: {port}", configuration.Hostname, configuration.Port);

            await eventBus.PublishAsync(new ConnectionEstablishedEvent());

            _socket.BeginReceive(_buffer, 0, _buffer.Length, 0, new AsyncCallback(ReceiveCallback), null);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error connecting to host {host} at port {port}", configuration.Hostname, configuration.Port);
            throw;
        }
    }

    public async Task SendPacket(PacketBase packet)
    {
        if (!_socket.Connected)
            return;

        using var stream = _streamManager.GetStream();
        var data = packetSerializer.SerializePacket(packet.GetType(), packet);

        var opCode = packetSerializer.GetOpCode(packet.GetType());
        if (opCode is null)
            return;

        stream.WriteVarInt(VarInt.ToBytesAsVarInt(opCode.Value).Length + data.Length);
        stream.WriteVarInt(opCode.Value);
        stream.Write(data);

        try
        {
            await _socket.SendAsync(stream.ToArray(), SocketFlags.None);

            logger.LogInformation("Sent packet of type {packetType}", packet.GetType().Name);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error sending packet with OP code {op} and size {size} to server", opCode, data.Length);
            throw;
        }
    }

    private void ReceiveCallback(IAsyncResult ar)
    {
        try
        {
            var bytesRead = _socket.EndReceive(ar);

            if (bytesRead > 0)
            {
                Array.Copy(_buffer, 0, _dataQueue, _currentQueuePosition, bytesRead);
                _currentQueuePosition += bytesRead;
                CheckForCompletePackets();
                _socket.BeginReceive(_buffer, 0, _buffer.Length, 0, new AsyncCallback(ReceiveCallback), null);
            }
            else
            {
                logger.LogWarning("Connection to server has been terminated");
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error receiving data from server");
            throw;
        }
    }

    private void CheckForCompletePackets()
    {
        while (_currentQueuePosition > 0)
        {
            using var stream = _streamManager.GetStream(_dataQueue);
            var packetLength = stream.ReadVarInt(); // This could be cached
            var totalLength = packetLength.ToBytesAsVarInt().Length + packetLength;

            if (_currentQueuePosition < totalLength)
                break;

            if (_compressionEnabled)
            {
                var dataLength = stream.ReadVarInt();
                if (dataLength != 0)
                    throw new NotImplementedException("Compression is not implemented yet!");
            }

            var opCode = stream.ReadVarInt();
            var data = new byte[packetLength - opCode.ToBytesAsVarInt().Length];
            stream.Read(data, 0, data.Length);

            _ = packetManager.HandlePacket(opCode, data);

            Array.Copy(_dataQueue, totalLength, _dataQueue, 0, _currentQueuePosition - totalLength);
            _currentQueuePosition -= totalLength;
        }
    }
}

