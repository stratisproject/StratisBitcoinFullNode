using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.SmartContracts.CLR
{
    public class ExecutionContext
    {
        public ExecutionContext(Observer observer)
        {
            this.Observer = observer;
        }

        public Observer Observer { get; }

        public IGasMeter GasMeter => this.Observer.GasMeter;
    }
}