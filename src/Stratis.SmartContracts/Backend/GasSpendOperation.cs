using System;

namespace Stratis.SmartContracts.Backend
{
    public struct GasSpendOperation
    {
        public GasSpendOperation(Gas cost)
        {
            this.Cost = cost;
        }

        public Gas Cost { get; }
    }
}