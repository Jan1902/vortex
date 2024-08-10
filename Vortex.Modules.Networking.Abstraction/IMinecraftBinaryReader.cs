using Vortex.Shared;

namespace Vortex.Modules.Networking.Abstraction;

public interface IMinecraftBinaryReader
{
    void Dispose();
    bool ReadBool();
    byte ReadByte();
    byte[] ReadBytes(int count);
    double ReadDouble();
    float ReadFloat();
    int ReadInt();
    long ReadLong();
    Vector3i ReadPosition();
    short ReadShort();
    string ReadStringWithIntPrefix();
    string ReadStringWithLength(int length);
    string ReadStringWithShortPrefix();
    string ReadStringWithVarIntPrefix();
    uint ReadUInt();
    ulong ReadULong();
    ushort ReadUShort();
    Guid ReadUUID();
    int ReadVarInt();
    bool[] ReadBitSet(int length);
}