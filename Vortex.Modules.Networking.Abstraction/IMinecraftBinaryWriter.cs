using Vortex.Shared;

namespace Vortex.Modules.Networking.Abstraction;

/// <summary>
/// Represents a binary writer for writing Minecraft data types to a stream.
/// </summary>
public interface IMinecraftBinaryWriter : IDisposable
{
    /// <summary>
    /// Gets the underlying stream.
    /// </summary>
    Stream UnderlyingStream { get; }

    /// <summary>
    /// Resets the position of the underlying stream to the beginning.
    /// </summary>
    void ResetStreamPosition();

    /// <summary>
    /// Writes a boolean value to the underlying stream.
    /// </summary>
    /// <param name="value">The boolean value to write.</param>
    void WriteBool(bool value);

    /// <summary>
    /// Writes a byte value to the underlying stream.
    /// </summary>
    /// <param name="value">The byte value to write.</param>
    void WriteByte(byte value);

    /// <summary>
    /// Writes an array of bytes to the underlying stream.
    /// </summary>
    /// <param name="bytes">The array of bytes to write.</param>
    void WriteBytes(byte[] bytes);

    /// <summary>
    /// Writes a double value to the underlying stream.
    /// </summary>
    /// <param name="value">The double value to write.</param>
    void WriteDouble(double value);

    /// <summary>
    /// Writes a float value to the underlying stream.
    /// </summary>
    /// <param name="value">The float value to write.</param>
    void WriteFloat(float value);

    /// <summary>
    /// Writes an integer value to the underlying stream.
    /// </summary>
    /// <param name="value">The integer value to write.</param>
    void WriteInt(int value);

    /// <summary>
    /// Writes a long value to the underlying stream.
    /// </summary>
    /// <param name="value">The long value to write.</param>
    void WriteLong(long value);

    /// <summary>
    /// Writes a Vector3i value to the underlying stream.
    /// </summary>
    /// <param name="value">The Vector3i value to write.</param>
    void WritePosition(Vector3i value);

    /// <summary>
    /// Writes a short value to the underlying stream.
    /// </summary>
    /// <param name="value">The short value to write.</param>
    void WriteShort(short value);

    /// <summary>
    /// Writes a string value with an integer prefix to the underlying stream.
    /// </summary>
    /// <param name="value">The string value to write.</param>
    void WriteStringWithIntPrefix(string value);

    /// <summary>
    /// Writes a string value with a short prefix to the underlying stream.
    /// </summary>
    /// <param name="value">The string value to write.</param>
    void WriteStringWithShortPrefix(string value);

    /// <summary>
    /// Writes a string value with a variable-length integer prefix to the underlying stream.
    /// </summary>
    /// <param name="value">The string value to write.</param>
    void WriteStringWithVarIntPrefix(string value);

    /// <summary>
    /// Writes an unsigned integer value to the underlying stream.
    /// </summary>
    /// <param name="value">The unsigned integer value to write.</param>
    void WriteUInt(uint value);

    /// <summary>
    /// Writes an unsigned long value to the underlying stream.
    /// </summary>
    /// <param name="value">The unsigned long value to write.</param>
    void WriteULong(ulong value);

    /// <summary>
    /// Writes an unsigned short value to the underlying stream.
    /// </summary>
    /// <param name="value">The unsigned short value to write.</param>
    void WriteUShort(ushort value);

    /// <summary>
    /// Writes a UUID value to the underlying stream.
    /// </summary>
    /// <param name="uuid">The UUID value to write.</param>
    void WriteUUID(Guid uuid);

    /// <summary>
    /// Writes a variable-length integer value to the underlying stream.
    /// </summary>
    /// <param name="value">The variable-length integer value to write.</param>
    void WriteVarInt(int value);

    /// <summary>
    /// Writes a bit set to the underlying stream.
    /// </summary>
    /// <param name="value">The bit set to write.</param>
    void WriteBitSet(bool[] value);

    /// <summary>
    /// Writes an NBT tag to the underlying stream.
    /// </summary>
    /// <param name="tag">The NBT tag to be written</param>
    void WriteNbtTag(NbtTag tag);
}
