using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.SmartContracts.CLR.Metering
{
    public class MemoryMeter : IMemoryMeter
    {
        public ulong MemoryAvailable { get; private set; }

        public ulong MemoryConsumed => this.MemoryLimit - this.MemoryAvailable;

        public ulong MemoryLimit { get; private set; }

        public MemoryMeter(ulong available)
        {
            this.MemoryLimit = available;
            this.MemoryAvailable = available;
        }

        public void Spend(ulong toSpend)
        {
            if (this.MemoryAvailable >= toSpend)
            {
                this.MemoryAvailable -= toSpend;
                return;
            }

            this.MemoryAvailable = 0;

            throw new MemoryConsumptionException($"Smart contract has allocated too much memory. Spent more than {this.MemoryLimit} memory units when allocating strings or arrays.");
        }
    }
}