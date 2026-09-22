using Vortex.Data;

namespace Vortex.Bot.Commands.Items;

/// <summary>
/// Sums up a pile of stacks for chat, such as <c>12x OakLog, 1x Stick</c>.
/// </summary>
internal static class ItemListing
{
    public static string Describe(IEnumerable<ItemStack?> stacks, string none)
    {
        var items = stacks.OfType<ItemStack>()
            .GroupBy(stack => stack.Item)
            .Select(group => $"{group.Sum(stack => stack.Count)}x {group.Key}")
            .ToList();

        return items.Count == 0 ? none : string.Join(", ", items);
    }
}
