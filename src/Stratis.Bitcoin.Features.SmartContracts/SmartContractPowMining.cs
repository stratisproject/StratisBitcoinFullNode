using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public class SmartContractPowMining : PowMining
    {
        private readonly CoinView coinView;
        private readonly SmartContractExecutorFactory executorFactory;
        private readonly ContractStateRepositoryRoot stateRoot;

        public SmartContractPowMining(
            IAsyncLoopFactory asyncLoopFactory,
            IConsensusLoop consensusLoop,
            ConcurrentChain chain,
            IDateTimeProvider dateTimeProvider,
            ITxMempool mempool,
            MempoolSchedulerLock mempoolLock,
            Network network,
            INodeLifetime nodeLifetime,
            ILoggerFactory loggerFactory,
            ContractStateRepositoryRoot stateRoot,
            CoinView coinView,
            SmartContractExecutorFactory executorFactory)
            : base(asyncLoopFactory, consensusLoop, chain, dateTimeProvider, mempool, mempoolLock, network, nodeLifetime, loggerFactory)
        {
            this.stateRoot = stateRoot;
            this.coinView = coinView;
            this.executorFactory = executorFactory;
        }

        protected override BlockTemplate GetBlockTemplate(ChainedBlock chainTip, ReserveScript reserveScript)
        {
            var asm = new SmartContractBlockAssembler(chainTip, this.consensusLoop, this.dateTimeProvider, this.loggerFactory, this.mempool, this.mempoolLock, this.network, this.stateRoot, this.executorFactory, this.coinView);
            return asm.CreateNewBlock(reserveScript.ReserveFullNodeScript);
        }
    }
}