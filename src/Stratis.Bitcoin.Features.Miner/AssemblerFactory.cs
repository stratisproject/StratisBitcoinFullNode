using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Miner
{
    /// <summary>
    /// Provides an interface for creating block templates of different types.
    /// </summary>
    public abstract class AssemblerFactory
    {
        public abstract BlockAssembler Create(ChainedBlock chainTip, AssemblerOptions options = null);
    }

    /// <summary>
    /// Provides functionality for creating PoW block templates.
    /// </summary>
    public class PowAssemblerFactory : AssemblerFactory
    {
        protected readonly ConsensusLoop consensusLoop;

        protected readonly Network network;

        protected readonly MempoolSchedulerLock mempoolLock;

        protected readonly ITxMempool mempool;

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
            ITxMempool mempool,
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

    /// <summary>
    /// Provides functionality for creating PoS block templates.
    /// </summary>
    public class PosAssemblerFactory : AssemblerFactory
    {
        protected readonly ConsensusLoop consensusLoop;

        protected readonly Network network;

        protected readonly MempoolSchedulerLock mempoolScheduler;

        protected readonly ITxMempool mempool;

        protected readonly IDateTimeProvider dateTimeProvider;

        protected readonly StakeChain stakeChain;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Provides functionality for checking validity of PoS blocks.</summary>
        private readonly StakeValidator stakeValidator;

        /// <summary>Factory for creating loggers.</summary>
        protected readonly ILoggerFactory loggerFactory;

        public PosAssemblerFactory(
            ConsensusLoop consensusLoop,
            Network network,
            MempoolSchedulerLock mempoolScheduler,
            ITxMempool mempool,
            StakeValidator stakeValidator,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            StakeChain stakeChain = null)
        {
            this.consensusLoop = consensusLoop;
            this.network = network;
            this.mempoolScheduler = mempoolScheduler;
            this.mempool = mempool;
            this.stakeValidator = stakeValidator;
            this.dateTimeProvider = dateTimeProvider;
            this.stakeChain = stakeChain;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public override BlockAssembler Create(ChainedBlock chainTip, AssemblerOptions options = null)
        {
            return new PosBlockAssembler(this.consensusLoop, this.network, this.mempoolScheduler, this.mempool,
                this.dateTimeProvider, this.stakeChain, this.stakeValidator, chainTip, this.loggerFactory, options);
        }
    }
}
