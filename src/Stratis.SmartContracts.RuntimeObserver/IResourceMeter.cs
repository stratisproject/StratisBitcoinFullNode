using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts.RuntimeObserver
{
    public interface IResourceMeter
    {
        ulong Available { get; }

        ulong Consumed { get; }

        ulong Limit { get; }

        void Spend(ulong toSpend);
    }
}
