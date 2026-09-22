using Vortex.Modules.Interaction.Abstraction;
using Vortex.Modules.Networking.Abstraction;

namespace Vortex.Modules.Interaction;

/// <summary>What an <see cref="Interact"/> does to the entity.</summary>
public enum InteractAction
{
    /// <summary>Uses it with the item in hand, as right-clicking does: trading, milking, riding.</summary>
    Interact = 0,

    /// <summary>Hits it.</summary>
    Attack = 1,

    /// <summary>Uses it at a particular point, as armour stands need.</summary>
    InteractAt = 2
}

/// <summary>
/// Acts on an entity.
/// </summary>
/// <remarks>
/// The point only goes along for <see cref="InteractAction.InteractAt"/>, the hand
/// only for the two using actions, which is why the packet is written by hand.
/// </remarks>
/// <param name="X">For <see cref="InteractAction.InteractAt"/>, where on the entity, relative to its position.</param>
/// <param name="Sneaking">Whether the player is sneaking, which changes what some entities do.</param>
[CustomSerialized<InteractSerializer, Interact>(PacketIds.Play.ServerBound.Interact, packetDirection: PacketDirection.ServerBound)]
public record Interact(int EntityId, InteractAction Action, float X, float Y, float Z, Hand Hand, bool Sneaking) : PacketBase;

internal class InteractSerializer : IPacketSerializer<Interact>
{
    public void SerializePacket(Interact packet, IMinecraftBinaryWriter writer)
    {
        writer.WriteVarInt(packet.EntityId);
        writer.WriteVarInt((int)packet.Action);

        if (packet.Action == InteractAction.InteractAt)
        {
            writer.WriteFloat(packet.X);
            writer.WriteFloat(packet.Y);
            writer.WriteFloat(packet.Z);
        }

        if (packet.Action != InteractAction.Attack)
            writer.WriteVarInt((int)packet.Hand);

        writer.WriteBool(packet.Sneaking);
    }

    public Interact DeserializePacket(IMinecraftBinaryReader reader)
        => throw new NotSupportedException("Only the client sends interactions.");
}
