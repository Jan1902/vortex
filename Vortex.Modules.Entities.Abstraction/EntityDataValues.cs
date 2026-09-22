using Vortex.Shared;

namespace Vortex.Modules.Entities.Abstraction;

/// <summary>A villager's type, profession and level, as registry IDs and a number.</summary>
public sealed record VillagerData(int Type, int Profession, int Level);

/// <summary>A block position in a particular dimension, such as where a player last died.</summary>
public sealed record GlobalPosition(string Dimension, Vector3i Position);
