namespace Vortex.Framework.Abstraction;

public interface IInitialize
{
    void Initialize();
}

public interface IInitializeAsync
{
    Task InitializeAsync();
}
