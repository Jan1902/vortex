using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vortex.Framework.Abstraction;

public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event);
}
