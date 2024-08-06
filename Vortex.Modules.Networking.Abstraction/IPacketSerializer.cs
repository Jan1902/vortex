namespace Vortex.Modules.Networking.Abstraction;

/// <summary>
/// Represents an interface for serializing and deserializing packets.
/// </summary>
/// <typeparam name="TPacket">The type of packet to be serialized and deserialized.</typeparam>
public interface IPacketSerializer<TPacket> where TPacket : PacketBase
{
    /// <summary>
    /// Serializes the specified packet.
    /// </summary>
    /// <param name="packet">The packet to be serialized.</param>
    void SerializePacket(TPacket packet, IMinecraftBinaryWriter writer);

    /// <summary>
    /// Deserializes the s.
    /// </summary>
    /// <returns>The deserialized packet.</returns>
    TPacket DeserializePacket(IMinecraftBinaryReader reader);
}
