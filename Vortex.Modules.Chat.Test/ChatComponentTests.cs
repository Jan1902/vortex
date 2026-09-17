using Vortex.Modules.Chat;
using Vortex.Shared;

namespace Vortex.Modules.Chat.Test;

public class ChatComponentTests
{
    [Fact]
    public void ReadsBareString()
        // Naming the argument is required: StringTag(string) binds to the
        // name-only constructor, which would silently leave the value unset.
        => Assert.Equal("Hello world!", ChatComponent.ToPlainText(new StringTag(value: "Hello world!")));

    [Fact]
    public void ReadsTextComponent()
    {
        var component = new CompoundTag([new StringTag("Hello world!", "text")]);

        Assert.Equal("Hello world!", ChatComponent.ToPlainText(component));
    }

    [Fact]
    public void AppendsExtraChildren()
    {
        var component = new CompoundTag(
        [
            new StringTag("Hello ", "text"),
            new ListTag(typeof(CompoundTag),
            [
                new CompoundTag([new StringTag("world", "text")]),
                new CompoundTag([new StringTag("!", "text")])
            ], "extra")
        ]);

        Assert.Equal("Hello world!", ChatComponent.ToPlainText(component));
    }

    [Fact]
    public void JoinsTranslationArguments()
    {
        // This is the shape of a /say message: chat.type.announcement with the
        // sender and the message as arguments.
        var component = new CompoundTag(
        [
            new StringTag("chat.type.announcement", "translate"),
            new ListTag(typeof(CompoundTag),
            [
                new CompoundTag([new StringTag("Server", "text")]),
                new CompoundTag([new StringTag("Hallo Jeff", "text")])
            ], "with")
        ]);

        Assert.Equal("Server: Hallo Jeff", ChatComponent.ToPlainText(component));
    }

    [Fact]
    public void FallsBackToTheTranslationKeyWithoutArguments()
    {
        var component = new CompoundTag([new StringTag("multiplayer.player.joined", "translate")]);

        Assert.Equal("multiplayer.player.joined", ChatComponent.ToPlainText(component));
    }

    [Fact]
    public void ReadsNestedComponents()
    {
        var component = new CompoundTag(
        [
            new StringTag("chat.type.text", "translate"),
            new ListTag(typeof(CompoundTag),
            [
                new CompoundTag([new StringTag("Jeff", "text")]),
                new CompoundTag(
                [
                    new StringTag("", "text"),
                    new ListTag(typeof(CompoundTag),
                    [
                        new CompoundTag([new StringTag("hello", "text")])
                    ], "extra")
                ])
            ], "with")
        ]);

        Assert.Equal("Jeff: hello", ChatComponent.ToPlainText(component));
    }

    [Fact]
    public void ReturnsEmptyForNull()
        => Assert.Equal(string.Empty, ChatComponent.ToPlainText(null));

    [Fact]
    public void ReturnsEmptyForAComponentWithoutContent()
        => Assert.Equal(string.Empty, ChatComponent.ToPlainText(new CompoundTag([new IntTag(5, "color")])));
}
