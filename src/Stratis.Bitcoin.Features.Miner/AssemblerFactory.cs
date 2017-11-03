using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;

namespace Stratis.Bitcoin.Features.Miner
{
    public abstract class AssemblerFactory
    {
        public abstract BlockAssembler Create(ChainedBlock chainTip, AssemblerOptions options = null);
    }

    public class PowAssemblerFactory : AssemblerFactory
    {
        protected readonly ConsensusLoop consensusLoop;
        protected readonly Network network;
        protected readonly MempoolSchedulerLock mempoolLock;
        protected readonly TxMempool mempool;
        protected readonly IDateTimeProvider dateTimeProvider;
        protected readonly StakeChain stakeChain;

        /// <summary>Factory for creating loggers.</summary>
        protected readonly ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        public PowAssemblerFactory(
            ConsensusLoop consensusLoop,
            Network network,
            MempoolSchedulerLock mempoolLock,
            TxMempool mempool,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            StakeChain stakeChain = null)
        {
            this.consensusLoop = consensusLoop;
            this.network = network;
            this.mempoolLock = mempoolLock;
            this.mempool = mempool;
            this.dateTimeProvider = dateTimeProvider;
            this.stakeChain = stakeChain;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public override BlockAssembler Create(ChainedBlock chainTip, AssemblerOptions options = null)
        {
            return new PowBlockAssembler(this.consensusLoop, this.network, this.mempoolLock, this.mempool, this.dateTimeProvider, chainTip, this.loggerFactory, options);
        }
    }

    public class PosAssemblerFactory : AssemblerFactory
    {
        protected readonly ConsensusLoop consensusLoop;
        protected readonly Network network;
        protected readonly MempoolSchedulerLock mempoolScheduler;
        protected readonly TxMempool mempool;
        protected readonly IDateTimeProvider dateTimeProvider;
        protected readonly StakeChain stakeChain;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Factory for creating loggers.</summary>
        protected readonly ILoggerFactory loggerFactory;

        public PosAssemblerFactory(
            ConsensusLoop consensusLoop,
            Network network,
            MempoolSchedulerLock mempoolScheduler,
            TxMempool mempool,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            StakeChain stakeChain = null)
        {
            this.consensusLoop = consensusLoop;
            this.network = network;
            this.mempoolScheduler = mempoolScheduler;
            this.mempool = mempool;
            this.dateTimeProvider = dateTimeProvider;
            this.stakeChain = stakeChain;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public override BlockAssembler Create(ChainedBlock chainTip, AssemblerOptions options = null)
        {
            return new PosBlockAssembler(this.consensusLoop, this.network, this.mempoolScheduler, this.mempool,
                this.dateTimeProvider, this.stakeChain, chainTip, this.loggerFactory, options);
        }
    }
}
