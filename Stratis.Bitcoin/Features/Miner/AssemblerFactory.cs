﻿using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;

namespace Stratis.Bitcoin.Features.Miner
{
    public abstract class AssemblerFactory
    {
        public abstract BlockAssembler Create(AssemblerOptions options = null);
    }

    public class PowAssemblerFactory : AssemblerFactory
    {
        protected readonly ConsensusLoop consensusLoop;
        protected readonly Network network;
        protected readonly ConcurrentChain chain;
        protected readonly MempoolAsyncLock mempoolLock;
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
            ConcurrentChain chain,
            MempoolAsyncLock mempoolLock,
            TxMempool mempool,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            StakeChain stakeChain = null)
        {
            this.consensusLoop = consensusLoop;
            this.network = network;
            this.chain = chain;
            this.mempoolLock = mempoolLock;
            this.mempool = mempool;
            this.dateTimeProvider = dateTimeProvider;
            this.stakeChain = stakeChain;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public override BlockAssembler Create(AssemblerOptions options = null)
        {
            return new PowBlockAssembler(this.consensusLoop, this.network, this.chain, this.mempoolLock, this.mempool, this.dateTimeProvider, this.loggerFactory, options);
        }
    }

    public class PosAssemblerFactory : AssemblerFactory
    {
        protected readonly ConsensusLoop consensusLoop;
        protected readonly Network network;
        protected readonly ConcurrentChain chain;
        protected readonly MempoolAsyncLock mempoolScheduler;
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
            ConcurrentChain chain,
            MempoolAsyncLock mempoolScheduler,
            TxMempool mempool,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            StakeChain stakeChain = null)
        {
            this.consensusLoop = consensusLoop;
            this.network = network;
            this.chain = chain;
            this.mempoolScheduler = mempoolScheduler;
            this.mempool = mempool;
            this.dateTimeProvider = dateTimeProvider;
            this.stakeChain = stakeChain;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public override BlockAssembler Create(AssemblerOptions options = null)
        {
            return new PosBlockAssembler(this.consensusLoop, this.network, this.chain, this.mempoolScheduler, this.mempool,
                this.dateTimeProvider, this.stakeChain, this.loggerFactory, options);
        }
    }
}
