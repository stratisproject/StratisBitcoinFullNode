﻿using Stratis.SmartContracts.CLR.Exceptions;
using Stratis.SmartContracts.RuntimeObserver;

namespace Stratis.SmartContracts.CLR
{
    public sealed class GasMeter : IResourceMeter
    {
        public ulong Available { get; private set; }

        public ulong Consumed => this.Limit - this.Available;

        public ulong Limit { get; }

        public GasMeter(ulong available)
        {
            this.Available = available;
            this.Limit = available;
        }

        public void Spend(ulong toSpend)
        {
            if (this.Available >= toSpend)
            {
                this.Available -= toSpend;
                return;
            }

            this.Available = 0;

            throw new OutOfGasException("Went over gas limit of " + this.Limit);
        }
    }
}