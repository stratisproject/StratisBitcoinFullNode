using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Miner
{
    /// <summary>
    /// Provides an interface for creating block templates of different types.
    /// </summary>
    public interface IAssemblerFactory
    {
        /// <summary>
        /// Creates a <see cref="BlockAssembler"/> which can be used to create new blocks.
        /// </summary>
        /// <param name="chainTip">The tip of the chain that this instance will work with without touching any shared chain resources.</param>
        /// <param name="options">The block assembler options.</param>
        /// <returns>A new block assembler.</returns>
        BlockAssembler Create(ChainedBlock chainTip, AssemblerOptions options = null);
    }

    /// <summary>
    /// Provides functionality for creating PoW block templates.
    /// </summary>
    public class PowAssemblerFactory : IAssemblerFactory
    {
        protected readonly IConsensusLoop consensusLoop;

        protected readonly Network network;

        protected readonly MempoolSchedulerLock mempoolLock;

        protected readonly ITxMempool mempool;

        protected readonly IDateTimeProvider dateTimeProvider;

        protected readonly IStakeChain stakeChain;

        /// <summary>Factory for creating loggers.</summary>
        protected readonly ILoggerFactory loggerFactory;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        public PowAssemblerFactory(
            IConsensusLoop consensusLoop,
            Network network,
            MempoolSchedulerLock mempoolLock,
            ITxMempool mempool,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            IStakeChain stakeChain = null)
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

        public BlockAssembler Create(ChainedBlock chainTip, AssemblerOptions options = null)
        {
            return new PowBlockAssembler(this.consensusLoop, this.network, this.mempoolLock, this.mempool, this.dateTimeProvider, chainTip, this.loggerFactory, options);
        }
    }

    /// <summary>
    /// Provides functionality for creating PoS block templates.
    /// </summary>
    public class PosAssemblerFactory : IAssemblerFactory
    {
        protected readonly IConsensusLoop consensusLoop;

        protected readonly Network network;

        protected readonly MempoolSchedulerLock mempoolScheduler;

        protected readonly ITxMempool mempool;

        protected readonly IDateTimeProvider dateTimeProvider;

        protected readonly IStakeChain stakeChain;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Provides functionality for checking validity of PoS blocks.</summary>
        private readonly IStakeValidator stakeValidator;

        /// <summary>Factory for creating loggers.</summary>
        protected readonly ILoggerFactory loggerFactory;

        public PosAssemblerFactory(
            IConsensusLoop consensusLoop,
            Network network,
            MempoolSchedulerLock mempoolScheduler,
            ITxMempool mempool,
            IStakeValidator stakeValidator,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            IStakeChain stakeChain = null)
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

        public BlockAssembler Create(ChainedBlock chainTip, AssemblerOptions options = null)
        {
            return new PosBlockAssembler(this.consensusLoop, this.network, this.mempoolScheduler, this.mempool,
                this.dateTimeProvider, this.stakeChain, this.stakeValidator, chainTip, this.loggerFactory, options);
        }
    }
}
