using Vortex.Bot.Commands.Control;
using Vortex.Bot.Commands.Infrastructure;
using Vortex.Data;
using Vortex.Framework.Abstraction;
using Vortex.Shared;

namespace Vortex.Bot.Test;

public class CommandTests
{
    private readonly List<string> _replies = [];

    [Fact]
    public async Task FillsArgumentsInOrder()
    {
        await Run("jeff put oak_log 1 64,5 -2.5");

        Assert.Equal(["OakLog at 1 64 -3, 1 times"], _replies);
    }

    [Fact]
    public async Task KeepsTheDefaultOfALeftOutOptionalArgument()
    {
        await Run("jeff put minecraft:stone 0 0 0 5");
        await Run("jeff put stone 0 0 0");

        Assert.Equal(["Stone at 0 0 0, 5 times", "Stone at 0 0 0, 1 times"], _replies);
    }

    [Theory]
    [InlineData("jeff put stone 0 0")]
    [InlineData("jeff put 0 0 0 stone")]
    [InlineData("jeff put not_an_item 0 0 0")]
    [InlineData("jeff put stone 0 0 0 5 6")]
    [InlineData("jeff put stone 0 0 0 five")]
    public async Task ShowsTheUsageWhenTheArgumentsDoNotFit(string message)
    {
        await Run(message);

        Assert.Equal(["Usage: jeff put <item> <x y z> [count]"], _replies);
    }

    [Fact]
    public async Task IgnoresMessagesForSomeoneElse()
    {
        await Run("bob put stone 0 0 0");
        await Run("hello jeff");

        Assert.Empty(_replies);
    }

    [Fact]
    public async Task IgnoresItsOwnMessages()
    {
        await Dispatcher().RunAsync(new ChatMessageReceivedEventArgs("jeff put <item> <x y z> [count]: Puts", Guid.NewGuid(), "Jeff"));

        Assert.Empty(_replies);
    }

    [Fact]
    public async Task ReadsTheNameInAnyCase()
    {
        await Run("Jeff PUT Stone 0 0 0");

        Assert.Equal(["Stone at 0 0 0, 1 times"], _replies);
    }

    [Fact]
    public async Task PointsAtHelpForAnUnknownCommand()
    {
        await Run("jeff dance");

        Assert.Equal(["I don't know how to dance. Try jeff help"], _replies);
    }

    [Fact]
    public async Task ReportsACommandThatFails()
    {
        await Run("jeff fail");

        Assert.Equal(["That went wrong: broken"], _replies);
    }

    [Fact]
    public async Task DoesNotWaitForACommandToFinish()
    {
        var dispatcher = Dispatcher();

        await dispatcher.HandleAsync(new ChatMessageReceivedEventArgs("jeff wait"));
        await dispatcher.HandleAsync(new ChatMessageReceivedEventArgs("jeff put stone 0 0 0"));

        Assert.Equal(["Waiting", "Stone at 0 0 0, 1 times"], _replies);

        WaitCommand.Release.SetResult();
    }

    [Fact]
    public async Task ExplainsACommand()
    {
        await Run("jeff help put");
        await Run("jeff help");

        Assert.Equal(
            ["jeff put <item> <x y z> [count]: Puts something somewhere", "I can: fail, help, put, wait. Try jeff help <command>"],
            _replies);
    }

    [Fact]
    public void NamesArgumentsInWords()
        => Assert.Equal("attack <entity type>", CommandInfo.For(typeof(Commands.Actions.AttackCommand)).Usage);

    [Fact]
    public void EveryBotCommandIsWellFormed()
    {
        var commands = CommandDispatcher.Discover(typeof(HelpCommand).Assembly).ToList();

        Assert.Contains(commands, command => command.Usage == "take <item> [count]");
        Assert.Equal(commands.Count, commands.Select(command => command.Name).Distinct().Count());
    }

    [Theory]
    [InlineData(typeof(GapInPositions))]
    [InlineData(typeof(OptionalBeforeRequired))]
    [InlineData(typeof(UnsupportedType))]
    public void RejectsMalformedCommands(Type type)
        => Assert.Throws<InvalidOperationException>(() => CommandInfo.For(type));

    private Task Run(string message)
        => Dispatcher().RunAsync(new ChatMessageReceivedEventArgs(message, Guid.NewGuid(), "Steve"));

    // The test commands never touch the client.
    private CommandDispatcher Dispatcher()
        => new(null!, "Jeff", [.. new[] { typeof(PutCommand), typeof(FailCommand), typeof(WaitCommand), typeof(HelpCommand) }.Select(CommandInfo.For)], Reply);

    private Task Reply(string message)
    {
        _replies.Add(message);
        return Task.CompletedTask;
    }

    [Command("put", "Puts something somewhere")]
    private sealed class PutCommand : BotCommand
    {
        [Argument(0)]
        public Item Item { get; set; }

        [Argument(1)]
        public Vector3i Block { get; set; } = null!;

        [Argument(2, Optional = true)]
        public int Count { get; set; } = 1;

        public override Task ExecuteAsync(CommandContext context)
            => context.ReplyAsync($"{Item} at {Block.X} {Block.Y} {Block.Z}, {Count} times");
    }

    [Command("fail", "Fails")]
    private sealed class FailCommand : BotCommand
    {
        public override Task ExecuteAsync(CommandContext context)
            => throw new InvalidOperationException("broken");
    }

    [Command("wait", "Waits until released")]
    private sealed class WaitCommand : BotCommand
    {
        public static TaskCompletionSource Release { get; } = new();

        public override async Task ExecuteAsync(CommandContext context)
        {
            await context.ReplyAsync("Waiting");
            await Release.Task;
        }
    }

    [Command("gap", "")]
    private sealed class GapInPositions : BotCommand
    {
        [Argument(1)]
        public int Count { get; set; }

        public override Task ExecuteAsync(CommandContext context) => Task.CompletedTask;
    }

    [Command("optional", "")]
    private sealed class OptionalBeforeRequired : BotCommand
    {
        [Argument(0, Optional = true)]
        public int Count { get; set; }

        [Argument(1)]
        public Item Item { get; set; }

        public override Task ExecuteAsync(CommandContext context) => Task.CompletedTask;
    }

    [Command("unsupported", "")]
    private sealed class UnsupportedType : BotCommand
    {
        [Argument(0)]
        public DateTime When { get; set; }

        public override Task ExecuteAsync(CommandContext context) => Task.CompletedTask;
    }
}
