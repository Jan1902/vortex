using Autofac;
using Vortex.Framework.Abstraction;

namespace Vortex.Modules.Chat;

public class ChatModule : IModule
{
    public void Load(ContainerBuilder builder)
    {
        builder.RegisterType<ChatManager>().AsImplementedInterfaces();
        builder.RegisterType<ChatPacketHandler>().AsImplementedInterfaces();
    }
}
