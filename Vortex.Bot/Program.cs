using Vortex.Framework;

var client = new VortexClientBuilder()
    .ConnectTo("localhost", 25565)
    .Build();

await client.StartAsync();

//await client.SendChatMessage("Hello world!");

while (true)
{

}