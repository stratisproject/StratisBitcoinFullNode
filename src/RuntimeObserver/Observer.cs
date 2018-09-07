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

        public void OperationUp()
        {
            this.GasMeter.Spend((Gas) 1);
        }


    }
}
