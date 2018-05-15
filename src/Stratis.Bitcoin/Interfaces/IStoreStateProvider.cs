using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace Stratis.Bitcoin.Interfaces
{
    public interface IStoreStateProvider
    {
        ChainedHeader StoreTip { get; }
    }
}