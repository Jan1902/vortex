namespace Vortex.Modules.Networking.Abstraction;

/// <summary>
/// Represents the type of string length in a packet.
/// </summary>
public enum StringLengthType
{
    VarIntPrefix,
    Terminated,
    Fixed
}

/// <summary>
/// Specifies the length attribute for a string parameter in a packet.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public class StringLengthAttribute : Attribute
{
    /// <summary>
    /// Gets the type of string length.
    /// </summary>
    public StringLengthType Type { get; }

    /// <summary>
    /// Gets the fixed length of the string.
    /// </summary>
    public int? Length { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="StringLengthAttribute"/> class with the specified string length type.
    /// </summary>
    /// <param name="type">The type of string length.</param>
    public StringLengthAttribute(StringLengthType type)
    {
        Type = type;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StringLengthAttribute"/> class with the specified fixed length.
    /// </summary>
    /// <param name="length">The fixed length of the string.</param>
    public StringLengthAttribute(int length)
    {
        Type = StringLengthType.Fixed;
        Length = length;
    }
}

/// <summary>
/// Specifies that a packet is automatically serialized.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AutoSerializedPacketAttribute"/> class with the specified packet ID and protocol state.
/// </remarks>
/// <param name="packetId">The packet ID.</param>
/// <param name="state">The protocol state.</param>
[AttributeUsage(AttributeTargets.Class)]
public class AutoSerializedPacketAttribute(int packetId, ProtocolState state = ProtocolState.Play, PacketDirection packetDirection = PacketDirection.ClientBound) : PacketAttribute(packetId, state, packetDirection)
{
}

/// <summary>
/// Specifies that a packet is custom serialized using a specific serializer and packet type.
/// </summary>
/// <typeparam name="TSerializer">The type of the packet serializer.</typeparam>
/// <typeparam name="TPacket">The type of the packet.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="CustomSerializedAttribute{TSerializer, TPacket}"/> class with the specified packet ID and protocol state.
/// </remarks>
/// <param name="packetId">The packet ID.</param>
/// <param name="state">The protocol state.</param>
[AttributeUsage(AttributeTargets.Class)]
public class CustomSerializedAttribute<TSerializer, TPacket>(int packetId, ProtocolState state = ProtocolState.Play) : PacketAttribute(packetId, state)
    where TSerializer : IPacketSerializer<TPacket>
    where TPacket : PacketBase
{
}

/// <summary>
/// Specifies the packet ID and protocol state for a packet.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="PacketAttribute"/> class with the specified packet ID and protocol state.
/// </remarks>
/// <param name="packetId">The packet ID.</param>
/// <param name="state">The protocol state.</param>
[AttributeUsage(AttributeTargets.Class)]
public class PacketAttribute(int packetId, ProtocolState state = ProtocolState.Play, PacketDirection packetDirection = PacketDirection.ClientBound) : Attribute
{
    /// <summary>
    /// Gets the packet ID.
    /// </summary>
    public int PacketId { get; } = packetId;

    /// <summary>
    /// Gets the protocol state.
    /// </summary>
    public ProtocolState State { get; } = state;

    /// <summary>
    /// Gets the packet direction.
    /// </summary>
    public PacketDirection PacketDirection { get; } = packetDirection;
}

[AttributeUsage(AttributeTargets.Parameter)]
public class ConditionalAttribute(ConditionalType type = ConditionalType.PreviousBoolean) : Attribute
{
    public ConditionalType Type { get; } = type;
}

public enum ConditionalType
{
    PreviousBoolean
}

[AttributeUsage(AttributeTargets.Class)]
public class PacketModelAttribute : Attribute;