using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.SmartContracts;
using Stratis.Bitcoin.Features.SmartContracts.PoA;
using Stratis.Bitcoin.Mining;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Features.FederatedPeg.IntegrationTests.Utils
{
    /// <summary>
    /// Exact same as FederatedPegBlockDefinition, just gives the premine to a wallet for convenience.
    /// </summary>
    public class TestFederatedPegBlockDefinition : SmartContractPoABlockDefinition
    {
        public TestFederatedPegBlockDefinition(
            IBlockBufferGenerator blockBufferGenerator,
            ICoinView coinView,
            IConsensusManager consensusManager,
            IDateTimeProvider dateTimeProvider,
            IContractExecutorFactory executorFactory,
            ILoggerFactory loggerFactory,
            ITxMempool mempool,
            MempoolSchedulerLock mempoolLock,
            Network network,
            ISenderRetriever senderRetriever,
            IStateRepositoryRoot stateRoot,
            MinerSettings minerSettings)
            : base(blockBufferGenerator, coinView, consensusManager, dateTimeProvider, executorFactory, loggerFactory, mempool, mempoolLock, network, senderRetriever, stateRoot, minerSettings)
        {
        }

        public override BlockTemplate Build(ChainedHeader chainTip, Script scriptPubKey)
        {
            return base.Build(chainTip, scriptPubKey);
        }
    }
}