using Vortex.Framework;
using Vortex.Framework.Abstraction;
using Vortex.Shared;

var client = new VortexClientBuilder()
    .ConnectTo("localhost", 25565)
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

    if (parts[1] == "block" && parts.Length == 5)
    {
        var pos = new Vector3i(int.Parse(parts[2]), int.Parse(parts[3]), int.Parse(parts[4]));
        var block = client.GetBlock(pos);

        await client.SendChatMessage(block?.BlockName ?? "Nothing");
    }

    if (parts[1] == "chunk" && parts.Length == 5)
    {
        var pos = new Vector2i(int.Parse(parts[2]), int.Parse(parts[4]));
        var chunk = client.GetChunk(pos);

        int y = int.Parse(parts[3]);
        var section = chunk?.Sections[(y >> 4) + 4];

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
                var relY = y & 0xf;
                var block = section.States[x, relY, z];

                line += (block?.BlockName.Split(":")[1][0] ?? 'x') + " ";
            }
            text += line + "\n";
        }

        await client.SendChatMessage("look into da console");

        Console.WriteLine("Chunk {0} {1} {2}", pos.X, y / 16, pos.Z);
        Console.WriteLine("Rel layer: {0}", y & 0xf);
        Console.WriteLine(text);
    }
}

while (true)
{

}