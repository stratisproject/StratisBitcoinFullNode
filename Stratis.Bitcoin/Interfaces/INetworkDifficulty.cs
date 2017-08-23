using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.Interfaces
{
    public interface INetworkDifficulty
    {
        Target GetNetworkDifficulty();
    }
}
