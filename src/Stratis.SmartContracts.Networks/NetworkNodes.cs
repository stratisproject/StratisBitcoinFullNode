using System;
using System.Collections.Generic;
using System.Text;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.PoA;
using Stratis.Bitcoin.Features.SmartContracts.PoS;
using Stratis.Bitcoin.Features.SmartContracts.PoW;
using Stratis.Bitcoin.Features.SmartContracts.Wallet;

namespace Stratis.SmartContracts.Networks
{
    public static class NetworkNodes
    {
        public static IFullNodeBuilder GetPoWSmartContractNodeBuilder(NodeSettings settings)
        {
            return new FullNodeBuilder()
                .UseNodeSettings(settings)
                .UseBlockStore()
                .UseMempool()
                .AddRPC()
                .AddSmartContracts()
                .UseSmartContractPowConsensus()
                .UseSmartContractWallet()
                .UseSmartContractPowMining()
                .UseReflectionExecutor()
                .UseApi();
        }

        public static IFullNodeBuilder GetPoASmartContractNodeBuilder(NodeSettings settings)
        {
            return new FullNodeBuilder()
                .UseNodeSettings(settings)
                .UseBlockStore()
                .UseMempool()
                .AddRPC()
                .AddSmartContracts()
                .UseSmartContractPoAConsensus()
                .UseSmartContractPoAMining()
                .UseSmartContractWallet()
                .UseReflectionExecutor()
                .UseApi();
        }
    }
}
