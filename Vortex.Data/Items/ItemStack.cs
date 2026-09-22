namespace Vortex.Data;

/// <summary>
/// A number of one item, as in an inventory slot or lying on the ground.
/// </summary>
/// <remarks>
/// Beyond its item and count a stack can carry components: damage, enchantments,
/// a custom name and so on. Each is kept exactly as the server sent it, so the
/// stack can be sent back unchanged; the common ones are also available decoded,
/// such as <see cref="Damage"/>.
/// </remarks>
public sealed record ItemStack(Item Item, int Count)
{
    /// <summary>Components the stack has on top of what its item has by default.</summary>
    public IReadOnlyList<ItemComponent> Components { get; init; } = [];

    /// <summary>Components the item has by default that this stack does not.</summary>
    public IReadOnlyList<DataComponentType> RemovedComponents { get; init; } = [];

    /// <summary>Whether the stack differs from a plain stack of its item.</summary>
    public bool HasComponents => Components.Count > 0 || RemovedComponents.Count > 0;

    /// <summary>How much of its durability the item has used up; 0 for new or unbreakable items.</summary>
    public int Damage => GetNumber(DataComponentType.Damage) ?? 0;

    /// <summary>How many uses the item lasts in all, or 0 if it does not wear out.</summary>
    public int MaxDamage => GetNumber(DataComponentType.MaxDamage) ?? Item.MaxDamage();

    /// <summary>How many of it fit into one slot.</summary>
    public int MaxStackSize => GetNumber(DataComponentType.MaxStackSize) ?? Item.MaxStackSize();

    /// <summary>
    /// The item's enchantments and their levels, by the enchantment's registry
    /// ID. Enchantments are numbered by the server, which announces the numbers
    /// while configuring, so the IDs are not <see cref="Enchantment"/> values.
    /// </summary>
    public IReadOnlyDictionary<int, int> Enchantments
        => Get<IReadOnlyDictionary<int, int>>(DataComponentType.Enchantments) ?? new Dictionary<int, int>();

    /// <summary>The decoded value of a component, if the stack has it and it is of a kind that is decoded.</summary>
    public T? Get<T>(DataComponentType type) where T : class
        => Components.FirstOrDefault(component => component.Type == type)?.Value as T;

    /// <summary>The value of a component that is a number, if the stack has it.</summary>
    public int? GetNumber(DataComponentType type)
        => Components.FirstOrDefault(component => component.Type == type)?.Value is int value ? value : null;

    public bool Equals(ItemStack? other)
        => other is not null
            && Item == other.Item
            && Count == other.Count
            && Components.SequenceEqual(other.Components)
            && RemovedComponents.SequenceEqual(other.RemovedComponents);

    public override int GetHashCode()
        => HashCode.Combine(Item, Count, Components.Count, RemovedComponents.Count);
}

/// <summary>
/// One component of an <see cref="ItemStack"/>.
/// </summary>
/// <param name="Data">The component exactly as it was sent, without its type.</param>
/// <param name="Value">
/// The component decoded, for the kinds that are: numbers for damage and stack
/// sizes, the text of a name, a dictionary of enchantments. <c>null</c> for the
/// rest.
/// </param>
public sealed record ItemComponent(DataComponentType Type, byte[] Data, object? Value = null)
{
    public bool Equals(ItemComponent? other)
        => other is not null && Type == other.Type && Data.AsSpan().SequenceEqual(other.Data);

    public override int GetHashCode()
        => HashCode.Combine(Type, Data.Length);
}
