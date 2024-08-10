namespace Vortex.Framework.Abstraction;

/// <summary>
/// Represents an interface for initializing objects.
/// </summary>
public interface IInitialize
{
    /// <summary>
    /// Initializes the object.
    /// </summary>
    void Initialize();
}

/// <summary>
/// Represents an interface for asynchronously initializing objects.
/// </summary>
public interface IInitializeAsync
{
    /// <summary>
    /// Asynchronously initializes the object.
    /// </summary>
    Task InitializeAsync();
}
