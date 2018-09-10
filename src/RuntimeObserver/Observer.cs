using Stratis.SmartContracts;

namespace RuntimeObserver
{
    /// <summary>
    /// Is able to hold metrics about the current runtime.
    /// Currently tracking gas and some unit of memory.
    /// </summary>
    public class Observer
    {

        /// <summary>
        /// The limit for 'memory units'.
        /// 
        /// A 'memory unit' is spent for any operation that reserves a significant chunk of memory. 
        /// 
        /// Standard allocations for primitives aren't counted as memory units as they are trivially small
        /// and the instructions that cause them will run the contract out of gas eventually.
        /// 
        /// Likewise, string primitives and method 
        /// </summary>
        private readonly ulong memoryLimit;

        public IGasMeter GasMeter { get; }

        public ulong MemoryConsumed { get; }

        public Observer(IGasMeter gasMeter, ulong memoryLimit)
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
