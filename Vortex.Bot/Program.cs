using System.Globalization;
using Vortex.Framework;
using Vortex.Framework.Abstraction;
using Vortex.Modules.Behaviour.Tasks;
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

        await client.SendChatMessage(block?.BlockName ?? "Nothing");
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

                line += (block?.BlockName.Split(":")[1][0] ?? 'x') + " ";
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

// Keep the bot alive until the process is stopped. A spin loop here would burn
// a core and keep the process alive after its parent is gone.
await Task.Delay(Timeout.Infinite);

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
