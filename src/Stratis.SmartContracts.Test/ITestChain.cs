using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.PoA;
using Stratis.Bitcoin.Features.SmartContracts.Wallet;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Networks;

namespace Stratis.SmartContracts.Test
{
    public interface ITestChain
    {
        IReadOnlyList<string> Addresses { get; }

        /// <summary>
        /// Performs all required setup for network.
        /// </summary>
        void Initialize();
        
        /// <summary>
        /// Mine blocks on the network. Miner selection algorithm depends on consensus model.
        /// </summary>
        /// <param name="num">Number of blocks to mine.</param>
        void MineBlocks(int num);
    }
}
