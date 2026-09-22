namespace System.Runtime.CompilerServices;

/// <summary>
/// Lets records and <c>init</c> accessors compile against netstandard2.0, which
/// source generators are bound to and which predates them.
/// </summary>
internal static class IsExternalInit;
