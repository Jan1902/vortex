using System.Globalization;
using Vortex.Data;
using Vortex.Framework;
using Vortex.Framework.Abstraction;
using Vortex.Modules.Behaviour.Abstraction;
using Vortex.Modules.Behaviour.Tasks;
using Vortex.Modules.Entities.Abstraction;
using Vortex.Modules.Navigation.Abstraction;
using Vortex.Shared;

var client = new VortexClientBuilder()
    .ConnectTo("localhost", 25565)
    .WithVerboseLogging()
    .Build();

client.ChatMessageReceived += HandleChatMessage;
await client.StartAsync();

await client.SendChatMessage("Hello world!");

async Task HandleChatMessage(ChatMessageReceivedEventArgs chat)
{
    var parts = chat.Message.ToLower().Split(' ');
    if (parts.Length < 2)
        return;

    if (parts[0] != "jeff")
        return;

    if (parts[1] == "pos")
    {
        var position = client.Position;
        var block = position.ToBlockPosition();

        await client.SendChatMessage(
            $"I'm on block {Coordinates.Format(block)}, exactly {Coordinates.Format(position)}, "
            + (client.IsOnGround ? "standing on solid ground" : "falling"));
    }

    if (parts[1] == "jump")
    {
        client.Movement.Jump();

        await client.SendChatMessage("Hop!");
    }

    if (parts[1] == "reach")
    {
        if (Coordinates.ParseBlock(parts, 2) is not { } target)
        {
            await client.SendChatMessage("Which block? jeff reach <x> <y> <z>");
            return;
        }

        // The root task has to come from the container, because a task takes its
        // managers through the constructor. Inside the behaviour system tasks
        // build their dependencies from injected factories instead.
        var task = client.Brain.CreateTask<WithinReachTask>(target);

        await client.SendChatMessage($"On it: {task.Description}");

        var result = await client.Brain.RunAsync(task);

        await client.SendChatMessage(result.ToString());
    }

    if (parts[1] == "goto")
    {
        if (Coordinates.ParseBlock(parts, 2) is not { } target)
        {
            await client.SendChatMessage("Which block? jeff goto <x> <y> <z>");
            return;
        }

        // Same task, different abilities: this one is allowed to jump gaps.
        var task = client.Brain.CreateTask<GoToTask>(target, MovementCapabilities.Athletic);

        await client.SendChatMessage($"On it (jumping allowed): {task.Description}");

        var result = await client.Brain.RunAsync(task);

        await client.SendChatMessage(result.ToString());
    }

    if (parts[1] == "come")
    {
        // Heads for where the player stands right now, once; it does not follow.
        if (chat.SenderUuid is not { } sender)
        {
            await client.SendChatMessage("Only players can call me over");
            return;
        }

        if (client.Entities.Get(sender) is not { } player)
        {
            await client.SendChatMessage("I can't see you from here");
            return;
        }

        var task = client.Brain.CreateTask<GoToTask>(player.Position.ToBlockPosition(), MovementCapabilities.Athletic);

        await client.SendChatMessage($"Coming, {chat.SenderName ?? "on my way"}: {task.Description}");

        var result = await client.Brain.RunAsync(task);

        await client.SendChatMessage(result.ToString());
    }

    if (parts[1] == "near")
    {
        var nearby = client.Entities.Entities
            .OrderBy(entity => entity.Position.DistanceTo(client.Position))
            .Take(5)
            .Select(entity => string.Create(
                CultureInfo.InvariantCulture,
                $"{Describe(entity)} ({entity.Position.DistanceTo(client.Position):F1}m)"))
            .ToList();

        await client.SendChatMessage(nearby.Count == 0 ? "Nobody around" : string.Join(", ", nearby));
    }

    if (parts[1] == "collect")
    {
        // Everything lying within a radius of where the bot stands now.
        var radius = parts.Length > 2 && Coordinates.TryParseCoordinate(parts[2], out var given) ? given : 16;
        var task = client.Brain.CreateTask<CollectItemsTask>(client.Position, radius);

        await client.SendChatMessage($"On it: {task.Description}");

        var result = await client.Brain.RunAsync(task);

        await client.SendChatMessage(result.ToString());
    }

    if (parts[1] == "mine")
    {
        if (Coordinates.ParseBlock(parts, 2) is not { } target)
        {
            await client.SendChatMessage("Which block? jeff mine <x> <y> <z>");
            return;
        }

        var task = client.Brain.CreateTask<HarvestBlockTask>(target);

        await client.SendChatMessage($"On it: {task.Description}");

        var result = await client.Brain.RunAsync(task);

        await client.SendChatMessage(result.ToString());
    }

    if (parts[1] == "use")
    {
        if (Coordinates.ParseBlock(parts, 2) is not { } target)
        {
            await client.SendChatMessage("Which block? jeff use <x> <y> <z>");
            return;
        }

        var task = client.Brain.CreateTask<UseBlockTask>(target);

        await client.SendChatMessage($"On it: {task.Description}");

        var result = await client.Brain.RunAsync(task);

        await client.SendChatMessage(result.ToString());
    }

    if (parts[1] == "chest")
    {
        if (Coordinates.ParseBlock(parts, 2) is not { } target)
        {
            await client.SendChatMessage("Which block? jeff chest <x> <y> <z>");
            return;
        }

        var result = await client.Brain.RunAsync(client.Brain.CreateTask<OpenContainerTask>(target));

        if (result.IsFailure || client.Inventory.OpenContainer is not { } container)
        {
            await client.SendChatMessage(result.ToString());
            return;
        }

        var contents = container.Slots.Take(container.ContainerSize)
            .OfType<ItemStack>()
            .GroupBy(stack => stack.Item)
            .Select(group => $"{group.Sum(stack => stack.Count)}x {group.Key}")
            .ToList();

        await client.SendChatMessage($"{container.Type}: " + (contents.Count == 0 ? "empty" : string.Join(", ", contents)));
    }

    if (parts[1] == "take" || parts[1] == "store")
    {
        if (parts.Length < 3 || Items.Parse(parts[2]) is not { } item)
        {
            await client.SendChatMessage($"What? jeff {parts[1]} <item>" + (parts[1] == "take" ? " [count]" : ""));
            return;
        }

        var amount = parts.Length > 3 && int.TryParse(parts[3], out var given) ? given : 64;

        BotTask task = parts[1] == "take"
            ? client.Brain.CreateTask<TakeItemsTask>(item, client.Inventory.Count(item) + amount)
            : client.Brain.CreateTask<StoreItemsTask>(item);

        var result = await client.Brain.RunAsync(task);

        await client.SendChatMessage($"{task.Description}: {result}");
    }

    if (parts[1] == "close")
    {
        await client.Inventory.CloseContainerAsync();

        await client.SendChatMessage("Closed");
    }

    if (parts[1] == "inv")
    {
        var inventory = client.Inventory;
        var carried = inventory.Find(_ => true)
            .GroupBy(found => found.Stack.Item)
            .Select(group => $"{group.Sum(found => found.Stack.Count)}x {group.Key}")
            .ToList();

        var held = inventory.HeldItem is { } stack
            ? $"{stack.Item}" + (stack.MaxDamage > 0 ? $" ({stack.MaxDamage - stack.Damage}/{stack.MaxDamage})" : "")
            : "nothing";

        await client.SendChatMessage($"Holding {held}. " + (carried.Count == 0 ? "Carrying nothing" : "Carrying " + string.Join(", ", carried)));
    }

    if (parts[1] == "doing")
    {
        var stack = client.Brain.CurrentStack;

        await client.SendChatMessage(stack.Count == 0 ? "Nothing" : string.Join(" -> ", stack));
    }

    if (parts[1] == "stop")
    {
        client.Brain.Cancel();

        await client.SendChatMessage("Stopping");
    }

    if (parts[1] == "block")
    {
        if (Coordinates.ParseBlock(parts, 2) is not { } position)
        {
            await client.SendChatMessage("Which block? jeff block <x> <y> <z>");
            return;
        }

        var block = client.GetBlock(position);

        await client.SendChatMessage(block?.ToString() ?? "Nothing");
    }

    if (parts[1] == "chunk")
    {
        // Takes the block you are interested in, not the chunk it sits in --
        // every command here speaks block coordinates.
        if (Coordinates.ParseBlock(parts, 2) is not { } position)
        {
            await client.SendChatMessage("Which block? jeff chunk <x> <y> <z>");
            return;
        }

        var chunkPosition = new Vector2i(position.X >> 4, position.Z >> 4);
        var chunk = client.GetChunk(chunkPosition);

        var sectionIndex = (position.Y >> 4) + 4;
        var section = chunk is not null && sectionIndex >= 0 && sectionIndex < chunk.Sections.Length
            ? chunk.Sections[sectionIndex]
            : null;

        if (section is null)
        {
            await client.SendChatMessage("I don't have that chunk loaded");
            return;
        }

        var text = "";
        for (int x = 0; x < 16; x++)
        {
            var line = "";
            for (int z = 0; z < 16; z++)
            {
                var block = section.States[x, position.Y & 0xf, z];

                line += (block is null ? 'x' : char.ToLowerInvariant(block.Block.ToString()[0])) + " ";
            }
            text += line + "\n";
        }

        await client.SendChatMessage("look into da console");

        Console.WriteLine("Chunk {0} {1}, layer y={2}", chunkPosition.X, chunkPosition.Z, position.Y);
        Console.WriteLine(
            "Covers blocks {0}..{1} on X and {2}..{3} on Z",
            chunkPosition.X << 4, (chunkPosition.X << 4) + 15,
            chunkPosition.Z << 4, (chunkPosition.Z << 4) + 15);
        Console.WriteLine(text);
    }

    if (parts[1] == "human")
    {
        var random = new Random();
        for (int i = 0; i < 15; i++)
        {
            var randomPosition = client.Position + new Vector3d(random.NextDouble() * 15, 0, random.NextDouble() * 15);
            var task = client.Brain.CreateTask<GoToTask>(randomPosition.ToBlockPosition());
            await client.Brain.RunAsync(task);

            await Task.Delay(random.Next(500, 3000));
        }
    }
}

string Describe(Entity entity)
{
    if (entity.Type == EntityType.Player && client.Entities.GetPlayer(entity.Uuid) is { } player)
        return player.Name;

    if (entity.Item is { } stack)
        return $"{stack.Count}x {stack.Item}";

    return entity.Health is { } health
        ? string.Create(CultureInfo.InvariantCulture, $"{entity.Type} {health:F0}hp")
        : entity.Type.ToString();
}

// Keep the bot alive until the process is stopped. A spin loop here would burn
// a core and keep the process alive after its parent is gone.
await Task.Delay(Timeout.Infinite);

/// <summary>
/// Turns item names typed in chat, as the game writes them, into items.
/// </summary>
static class Items
{
    /// <summary>Reads <c>oak_log</c> or <c>minecraft:oak_log</c> as <see cref="Item.OakLog"/>.</summary>
    public static Item? Parse(string name)
    {
        var pascal = string.Concat(name.Replace("minecraft:", "")
            .Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..]));

        return Enum.TryParse<Item>(pascal, ignoreCase: true, out var item) ? item : null;
    }
}

/// <summary>
/// Turns what someone typed in chat into the block coordinates the bot works in.
/// </summary>
static class Coordinates
{
    /// <summary>
    /// Reads a block position from a command.
    /// </summary>
    /// <remarks>
    /// A whole number is a block coordinate and stands for itself. A coordinate
    /// with a fraction -- what the F3 screen shows, and what the bot reports as
    /// its exact position -- names a point in the world, and the block meant is
    /// the one containing it. Both go through the same rule, so 42.5 and 42 are
    /// both block 42, and -16.5 and -17 are both block -17.
    /// </remarks>
    /// <returns>The block, or null if three coordinates could not be read.</returns>
    public static Vector3i? ParseBlock(string[] parts, int offset)
    {
        if (parts.Length < offset + 3)
            return null;

        var coordinates = new double[3];

        for (var i = 0; i < 3; i++)
        {
            if (!TryParseCoordinate(parts[offset + i], out coordinates[i]))
                return null;
        }

        return new Vector3d(coordinates[0], coordinates[1], coordinates[2]).ToBlockPosition();
    }

    /// <summary>
    /// Reads one coordinate, written with either separator.
    /// </summary>
    /// <remarks>
    /// The game writes coordinates with a dot wherever it is running, so parsing
    /// is invariant rather than following the machine's locale. A comma is
    /// accepted as the same thing, because someone on a German keyboard will
    /// type one and the coordinates of a command are separated by spaces, so it
    /// cannot mean anything else here.
    /// </remarks>
    public static bool TryParseCoordinate(string text, out double value)
        => double.TryParse(
            text.Replace(',', '.'),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);

    public static string Format(Vector3i block)
        => $"{block.X} {block.Y} {block.Z}";

    public static string Format(Vector3d point)
        => string.Format(CultureInfo.InvariantCulture, "{0:F2} {1:F2} {2:F2}", point.X, point.Y, point.Z);
}
