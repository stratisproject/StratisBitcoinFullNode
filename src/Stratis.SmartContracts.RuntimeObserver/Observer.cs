namespace Stratis.SmartContracts.RuntimeObserver
{
    /// <summary>
    /// Is able to hold metrics about the current runtime.
    /// Currently tracking gas and some unit of memory.
    /// </summary>
    public class Observer
    {
        public IMemoryMeter MemoryMeter { get; private set; }

        public IGasMeter GasMeter { get; private set; }

        public Observer(IGasMeter gasMeter, IMemoryMeter memoryMeter)
        {
            this.GasMeter = gasMeter;
            this.MemoryMeter = memoryMeter;
        }

        /// <summary>
        /// Forwards the spending of gas to the GasMeter reference.
        /// </summary>
        public void SpendGas(long gas)
        {
            this.GasMeter.Spend((Gas)gas);
        }

        /// <summary>
        /// Register that some amount of memory has been reserved.
        /// </summary>
        public void SpendMemory(long memory)
        {
            this.MemoryMeter.Spend((ulong)memory);
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