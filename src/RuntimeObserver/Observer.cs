using Stratis.SmartContracts;

namespace RuntimeObserver
{
    public class Observer
    {
        public IGasMeter GasMeter { get; }

        private long operationCountLimit;

        public Observer(IGasMeter gasMeter)
        {
            this.GasMeter = gasMeter;
        }

        public void SpendGas(ulong gas)
        {
            this.GasMeter.Spend((Gas) gas);
        }
    }
}
