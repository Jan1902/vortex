namespace Vortex.Bot.Commands.Infrastructure;

/// <summary>
/// Makes a class a chat command, called as <c>jeff &lt;name&gt; ...</c>.
/// </summary>
/// <param name="name">What is typed after the bot's name.</param>
/// <param name="description">What the command does, for <c>jeff help</c>.</param>
[AttributeUsage(AttributeTargets.Class)]
public sealed class CommandAttribute(string name, string description) : Attribute
{
    public string Name { get; } = name;

    public string Description { get; } = description;
}

/// <summary>
/// Makes a property of a command one of its arguments, filled from the words
/// typed after the command's name.
/// </summary>
/// <param name="position">Where the argument comes, counting from 0.</param>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ArgumentAttribute(int position) : Attribute
{
    public int Position { get; } = position;

    /// <summary>
    /// Whether the argument can be left out, keeping the value the property
    /// starts with. Only trailing arguments can be optional.
    /// </summary>
    public bool Optional { get; init; }
}
