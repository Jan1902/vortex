using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Vortex.CodeGeneration;

/// <summary>
/// Lays out generated code consistently, so it reads well when stepped into or
/// opened from the analyzers node, whatever indentation the templates nested it at.
/// </summary>
internal static class CodeFormatter
{
    public static string Format(string code)
        => CSharpSyntaxTree.ParseText(code).GetRoot().NormalizeWhitespace(eol: "\n").ToFullString();
}
