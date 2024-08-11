namespace Vortex.Framework.Abstraction;

public delegate Task AsyncEventHandler(EventArgs e);

public delegate Task AsyncEventHandler<TEventArgs>(TEventArgs e);