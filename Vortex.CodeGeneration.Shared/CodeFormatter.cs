using System.Text;

namespace Vortex.CodeGeneration;

/// <summary>
/// Re-indents generated code, so it reads well when stepped into or opened from
/// the analyzers node, whatever depth the templates were nested at.
/// </summary>
/// <remarks>
/// Only indentation and blank lines change. Everything else stays as the
/// templates wrote it, including comments, which Roslyn's
/// <c>NormalizeWhitespace</c> would move around.
/// </remarks>
internal static class CodeFormatter
{
    private const string Indentation = "    ";

    public static string Format(string code)
    {
        var builder = new StringBuilder();
        var depth = 0;
        var opened = true;
        var pendingBlank = false;

        foreach (var rawLine in code.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.Trim();

            // Empty placeholders leave blank lines behind. Keep at most one in a
            // row, and none right inside a pair of braces.
            if (line.Length == 0)
            {
                pendingBlank = !opened;
                continue;
            }

            var closing = CountLeadingClosers(line);

            if (pendingBlank && closing == 0)
                builder.Append('\n');

            for (var i = 0; i < depth - closing; i++)
                builder.Append(Indentation);

            builder.Append(line).Append('\n');

            var change = CountBracketChange(line);
            depth += change;
            opened = change > 0;
            pendingBlank = false;
        }

        return builder.ToString();
    }

    private static int CountLeadingClosers(string line)
    {
        var count = 0;

        foreach (var character in line)
        {
            if (character is '}' or ']')
                count++;
            else if (character is not (',' or ';' or ')' or ' '))
                break;
        }

        return count;
    }

    /// <summary>
    /// How much deeper the next line sits: opening braces and brackets minus
    /// closing ones, ignoring any inside strings, characters and comments.
    /// </summary>
    private static int CountBracketChange(string line)
    {
        var change = 0;

        for (var i = 0; i < line.Length; i++)
        {
            var character = line[i];

            if (character == '/' && i + 1 < line.Length && line[i + 1] == '/')
                break;

            if (character == '/' && i + 1 < line.Length && line[i + 1] == '*')
            {
                var end = line.IndexOf("*/", i + 2, System.StringComparison.Ordinal);
                if (end < 0)
                    break;

                i = end + 1;
                continue;
            }

            if (character is '"' or '\'')
            {
                for (i++; i < line.Length && line[i] != character; i++)
                    if (line[i] == '\\')
                        i++;

                continue;
            }

            if (character is '{' or '[')
                change++;
            else if (character is '}' or ']')
                change--;
        }

        return change;
    }
}
