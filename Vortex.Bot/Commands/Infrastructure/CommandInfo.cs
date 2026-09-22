using System.Reflection;
using System.Text.RegularExpressions;

namespace Vortex.Bot.Commands.Infrastructure;

/// <summary>
/// What is known about a command class: how it is called, what arguments it
/// takes, and how to make an instance of it from the words typed.
/// </summary>
public sealed partial class CommandInfo
{
    private CommandInfo(Type type, string name, string description, IReadOnlyList<ArgumentInfo> arguments)
    {
        Type = type;
        Name = name;
        Description = description;
        Arguments = arguments;
        Usage = string.Join(' ', [name, .. arguments.Select(argument => argument.Usage)]);
    }

    public Type Type { get; }

    /// <summary>What is typed after the bot's name.</summary>
    public string Name { get; }

    public string Description { get; }

    public IReadOnlyList<ArgumentInfo> Arguments { get; }

    /// <summary>How the command is written, such as <c>take &lt;item&gt; [count]</c>.</summary>
    public string Usage { get; }

    /// <summary>
    /// Reads a command class.
    /// </summary>
    /// <exception cref="InvalidOperationException">The class is not a well-formed command.</exception>
    public static CommandInfo For(Type type)
    {
        if (!typeof(BotCommand).IsAssignableFrom(type) || type.IsAbstract || type.GetConstructor(Type.EmptyTypes) is null)
            throw new InvalidOperationException($"{type.Name} is not a command: it has to be a concrete {nameof(BotCommand)} with a parameterless constructor");

        var command = type.GetCustomAttribute<CommandAttribute>()
            ?? throw new InvalidOperationException($"{type.Name} has no [{nameof(CommandAttribute)}]");

        var arguments = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => (Property: property, Argument: property.GetCustomAttribute<ArgumentAttribute>()))
            .Where(found => found.Argument is not null)
            .OrderBy(found => found.Argument!.Position)
            .Select(found => new ArgumentInfo(found.Property, found.Argument!.Optional))
            .ToList();

        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i];

            if (argument.Property.GetCustomAttribute<ArgumentAttribute>()!.Position != i)
                throw new InvalidOperationException($"{type.Name}: argument positions have to count up from 0 without gaps");

            if (!argument.Property.CanWrite)
                throw new InvalidOperationException($"{type.Name}.{argument.Property.Name} has no setter");

            if (!ArgumentParsers.Supports(argument.Type))
                throw new InvalidOperationException($"{type.Name}.{argument.Property.Name}: there is no parser for {argument.Type.Name}");

            if (i > 0 && arguments[i - 1].Optional && !argument.Optional)
                throw new InvalidOperationException($"{type.Name}.{argument.Property.Name}: only trailing arguments can be optional");
        }

        return new CommandInfo(type, command.Name, command.Description, arguments);
    }

    /// <summary>
    /// Makes an instance of the command with its arguments filled from the
    /// words after its name.
    /// </summary>
    /// <returns>The command, or <c>null</c> if the words do not fit its arguments.</returns>
    public BotCommand? Bind(IReadOnlyList<string> words)
    {
        var command = (BotCommand)Activator.CreateInstance(Type)!;
        var index = 0;

        foreach (var argument in Arguments)
        {
            if (index == words.Count && argument.Optional)
                break;

            if (ArgumentParsers.Parse(argument.Type, words, index) is not { } parsed)
                return null;

            argument.Property.SetValue(command, parsed.Value);
            index += parsed.Words;
        }

        return index == words.Count ? command : null;
    }

    /// <summary>
    /// One argument of a command.
    /// </summary>
    public sealed partial class ArgumentInfo(PropertyInfo property, bool optional)
    {
        public PropertyInfo Property { get; } = property;

        public bool Optional { get; } = optional;

        /// <summary>The type of value the argument takes, without a <see cref="Nullable{T}"/> around it.</summary>
        public Type Type => Nullable.GetUnderlyingType(Property.PropertyType) ?? Property.PropertyType;

        /// <summary>The argument's name as the caller reads it: <c>EntityType</c> is <c>entity type</c>.</summary>
        public string Name => WordStart().Replace(Property.Name, " $1").ToLowerInvariant();

        /// <summary>How the argument is written in a usage line: <c>&lt;item&gt;</c>, or <c>[count]</c> if it is optional.</summary>
        public string Usage
        {
            get
            {
                var usage = ArgumentParsers.Usage(Type, Name);

                return Optional ? $"[{usage}]" : $"<{usage}>";
            }
        }

        [GeneratedRegex("(?<!^)([A-Z])")]
        private static partial Regex WordStart();
    }
}
