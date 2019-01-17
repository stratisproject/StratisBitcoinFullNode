using System;
using System.Collections.Generic;
using System.Text;
using Stratis.SmartContracts.CLR.Exceptions;
using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.SmartContracts.CLR
{
    public class MemoryMeter : IResourceMeter
    {
        public ulong Available { get; private set; }

        public ulong Consumed => this.Limit - this.Available;

        public ulong Limit { get; private set; }

        public MemoryMeter(ulong available)
        {
            this.Limit = available;
            this.Available = available;
        }

        public void Spend(ulong toSpend)
        {
            if (this.Available >= toSpend)
            {
                this.Available -= toSpend;
                return;
            }

            this.Available = 0;

            throw new MemoryConsumptionException($"Smart contract has allocated too much memory. Spent more than {this.Limit} memory units when allocating strings or arrays.");
        }
    }
}
