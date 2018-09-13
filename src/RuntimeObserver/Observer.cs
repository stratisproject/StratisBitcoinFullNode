using Stratis.SmartContracts;

namespace RuntimeObserver
{
    /// <summary>
    /// Is able to hold metrics about the current runtime..
    /// For now only tracking gas but in the future could be used to track memory or other metrics too. 
    /// </summary>
    public class Observer
    {
        public IGasMeter GasMeter { get; }

        public Observer(IGasMeter gasMeter)
        {
            this.GasMeter = gasMeter;
        }

        /// <summary>
        /// Forwards the spending of gas to the GasMeter reference.
        /// </summary>
        public void SpendGas(ulong gas)
        {
            this.GasMeter.Spend((Gas) gas);
        }
    }
}
