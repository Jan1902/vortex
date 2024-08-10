using Vortex.Shared;

namespace Vortex.Modules.Networking.Abstraction;

/// <summary>
/// Represents a binary reader for reading Minecraft data.
/// </summary>
public interface IMinecraftBinaryReader
{
    /// <summary>
    /// Releases all resources used by the binary reader.
    /// </summary>
    void Dispose();

    /// <summary>
    /// Reads a boolean value from the binary stream.
    /// </summary>
    /// <returns>The boolean value read from the stream.</returns>
    bool ReadBool();

    /// <summary>
    /// Reads a byte value from the binary stream.
    /// </summary>
    /// <returns>The byte value read from the stream.</returns>
    byte ReadByte();

    /// <summary>
    /// Reads a specified number of bytes from the binary stream.
    /// </summary>
    /// <param name="count">The number of bytes to read.</param>
    /// <returns>An array of bytes read from the stream.</returns>
    byte[] ReadBytes(int count);

    /// <summary>
    /// Reads a double value from the binary stream.
    /// </summary>
    /// <returns>The double value read from the stream.</returns>
    double ReadDouble();

    /// <summary>
    /// Reads a float value from the binary stream.
    /// </summary>
    /// <returns>The float value read from the stream.</returns>
    float ReadFloat();

    /// <summary>
    /// Reads an integer value from the binary stream.
    /// </summary>
    /// <returns>The integer value read from the stream.</returns>
    int ReadInt();

    /// <summary>
    /// Reads a long value from the binary stream.
    /// </summary>
    /// <returns>The long value read from the stream.</returns>
    long ReadLong();

    /// <summary>
    /// Reads a Vector3i value from the binary stream.
    /// </summary>
    /// <returns>The Vector3i value read from the stream.</returns>
    Vector3i ReadPosition();

    /// <summary>
    /// Reads a short value from the binary stream.
    /// </summary>
    /// <returns>The short value read from the stream.</returns>
    short ReadShort();

    /// <summary>
    /// Reads a string with an integer prefix from the binary stream.
    /// </summary>
    /// <returns>The string value read from the stream.</returns>
    string ReadStringWithIntPrefix();

    /// <summary>
    /// Reads a string with a specified length from the binary stream.
    /// </summary>
    /// <param name="length">The length of the string to read.</param>
    /// <returns>The string value read from the stream.</returns>
    string ReadStringWithLength(int length);

    /// <summary>
    /// Reads a string with a short prefix from the binary stream.
    /// </summary>
    /// <returns>The string value read from the stream.</returns>
    string ReadStringWithShortPrefix();

    /// <summary>
    /// Reads a string with a variable-length integer prefix from the binary stream.
    /// </summary>
    /// <returns>The string value read from the stream.</returns>
    string ReadStringWithVarIntPrefix();

    /// <summary>
    /// Reads an unsigned integer value from the binary stream.
    /// </summary>
    /// <returns>The unsigned integer value read from the stream.</returns>
    uint ReadUInt();

    /// <summary>
    /// Reads an unsigned long value from the binary stream.
    /// </summary>
    /// <returns>The unsigned long value read from the stream.</returns>
    ulong ReadULong();

    /// <summary>
    /// Reads an unsigned short value from the binary stream.
    /// </summary>
    /// <returns>The unsigned short value read from the stream.</returns>
    ushort ReadUShort();

    /// <summary>
    /// Reads a UUID value from the binary stream.
    /// </summary>
    /// <returns>The UUID value read from the stream.</returns>
    Guid ReadUUID();

    /// <summary>
    /// Reads a variable-length integer value from the binary stream.
    /// </summary>
    /// <returns>The variable-length integer value read from the stream.</returns>
    int ReadVarInt();

    /// <summary>
    /// Reads a bit set of a specified length from the binary stream.
    /// </summary>
    /// <param name="length">The length of the bit set to read.</param>
    /// <returns>An array of booleans representing the bit set read from the stream.</returns>
    bool[] ReadBitSet(int length);
}
