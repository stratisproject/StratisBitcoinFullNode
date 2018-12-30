namespace Stratis.SmartContracts.RuntimeObserver
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
        /// String primitives and method parameters are always limited by the size of transactions and will be costly.
        /// 
        /// So 'memory units' are spent when allocating arrays or performing string concatenations.
        /// </summary>
        private readonly long memoryLimit;

        public IGasMeter GasMeter { get; }

        public long MemoryConsumed { get; private set; }

        public Observer(IGasMeter gasMeter, long memoryLimit)
        {
            this.GasMeter = gasMeter;
            this.memoryLimit = memoryLimit;
        }

        /// <summary>
        /// Forwards the spending of gas to the GasMeter reference.
        /// </summary>
        public void SpendGas(long gas)
        {
            this.GasMeter.Spend((Gas) gas);
        }

        /// <summary>
        /// Register that some amount of memory has been reserved. If it goes over the allowed limit, throw an exception.
        /// </summary>
        public void SpendMemory(long memory)
        {
            this.MemoryConsumed += memory;

            if (this.MemoryConsumed > this.memoryLimit)
                throw new MemoryConsumptionException($"Smart contract has allocated too much memory. Spent more than {this.memoryLimit} memory units when allocating strings or arrays.");
        }

        /// <summary>
        /// Allows parameters to come in and be silently checked, and then returned onto the stack.
        /// </summary>
        public static class FlowThrough
        {
            public static int SpendMemoryInt32(int count, Observer observer)
            {
                observer.SpendMemory(count);
                return count;
            }
        }
    }
}
