using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.RuntimeObserver
{
    public interface IMemoryMeter
    {
        ulong MemoryAvailable { get; }

        ulong MemoryConsumed { get; }

        ulong MemoryLimit { get; }

        void Spend(ulong toSpend);
    }
}
