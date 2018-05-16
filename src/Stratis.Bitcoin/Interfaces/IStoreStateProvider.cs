using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace Stratis.Bitcoin.Interfaces
{
    public interface IStoreStateProvider
    {
        /// <summary>Represents the last block stored to disk.</summary>
        ChainedHeader HighestPersistedBlock { get; }
    }
}