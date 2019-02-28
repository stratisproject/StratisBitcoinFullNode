using Stratis.Bitcoin.EventBus;

namespace Stratis.Bitcoin.Signals
{
    public interface ISignals : IEventBus
    {
    }

    public class Signals : InMemoryEventBus, ISignals
    {
    }
}
