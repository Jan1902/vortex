namespace Vortex.Data;

/// <summary>
/// A number of one item, as in an inventory slot or lying on the ground.
/// </summary>
/// <param name="HasComponents">
/// Whether the stack carries data beyond its item and count, such as
/// enchantments or a custom name. That data is not read.
/// </param>
public sealed record ItemStack(Item Item, int Count, bool HasComponents = false);
