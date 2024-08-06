using Vortex.Framework;
using Vortex.Modules.Chat;

var client = new VortexClientBuilder()
    .AddModule<ChatModule>()
    .ConnectTo("localhost", 25565)
    .Build();

await client.StartAsync();