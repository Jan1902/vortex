using System.Text;
using Vortex.Shared;

namespace Vortex.Modules.Chat;

/// <summary>
/// Flattens a chat text component into readable plain text.
/// </summary>
/// <remarks>
/// Since 1.20.3 the server sends chat as an NBT text component rather than a
/// string. A component is either a plain string, or a compound carrying one of
/// several content types plus an optional list of children under "extra".
/// Formatting and styling are dropped, only the text survives.
/// </remarks>
public static class ChatComponent
{
    /// <summary>
    /// Extracts the plain text of a chat component.
    /// </summary>
    /// <param name="tag">The component, as received in a packet.</param>
    /// <returns>The readable text, which is empty if the component carried none.</returns>
    public static string ToPlainText(NbtTag? tag)
    {
        var builder = new StringBuilder();

        Append(tag, builder);

        return builder.ToString();
    }

    private static void Append(NbtTag? tag, StringBuilder builder)
    {
        switch (tag)
        {
            case null:
                return;

            // A bare string is a valid component on its own.
            case StringTag stringTag:
                builder.Append(stringTag.Value);
                return;

            case ListTag listTag:
                foreach (var item in listTag.Items)
                    Append(item, builder);
                return;

            case CompoundTag compound:
                AppendCompound(compound, builder);
                return;
        }
    }

    private static void AppendCompound(CompoundTag compound, StringBuilder builder)
    {
        // Literal text.
        if (Find(compound, "text") is StringTag text)
        {
            builder.Append(text.Value);
        }
        // A translated message. Without the language files the key cannot be
        // resolved, so the arguments are joined instead, which is what carries
        // the actual information for the common chat formats.
        else if (Find(compound, "translate") is StringTag translate)
        {
            var arguments = Find(compound, "with");

            if (arguments is ListTag { Items: var items } && items.Any())
                AppendJoined(items, builder);
            else
                builder.Append(translate.Value);
        }
        // A scoreboard value or a keybind, both of which only carry a name.
        else if (Find(compound, "keybind") is StringTag keybind)
        {
            builder.Append(keybind.Value);
        }

        // Children are appended to whatever the parent produced.
        if (Find(compound, "extra") is ListTag extra)
            foreach (var child in extra.Items)
                Append(child, builder);
    }

    private static void AppendJoined(IEnumerable<NbtTag> items, StringBuilder builder)
    {
        var first = true;

        foreach (var item in items)
        {
            if (!first)
                builder.Append(": ");

            Append(item, builder);
            first = false;
        }
    }

    private static NbtTag? Find(CompoundTag compound, string name)
        => compound.Children.FirstOrDefault(c => c.Name == name);
}
