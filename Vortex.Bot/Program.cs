using Vortex.Bot.Commands.Infrastructure;
using Vortex.Framework;

const string name = "Jeff";

var client = new VortexClientBuilder()
    .ConnectTo("localhost", 25565)
    .WithUsername(name)
    .WithVerboseLogging()
    .Build();

var commands = new CommandDispatcher(client, name, CommandDispatcher.Discover(typeof(Program).Assembly));
client.ChatMessageReceived += commands.HandleAsync;

var stopped = new TaskCompletionSource();
Console.CancelKeyPress += (_, args) =>
{
    // Let the bot leave the server instead of the process dying under it.
    args.Cancel = true;
    stopped.TrySetResult();
};

await client.StartAsync();
await client.SendChatMessage("Hello world!");

await stopped.Task;
await client.StopAsync();
