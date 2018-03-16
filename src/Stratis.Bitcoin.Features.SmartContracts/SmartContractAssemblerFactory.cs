using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.ContractValidation;
using Stratis.SmartContracts.Core.State;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public class SmartContractAssemblerFactory : IAssemblerFactory
    {
        protected readonly IConsensusLoop consensusLoop;

        protected readonly Network network;

        protected readonly MempoolSchedulerLock mempoolScheduler;

        protected readonly ITxMempool mempool;

        protected readonly IDateTimeProvider dateTimeProvider;

        protected readonly StakeChain stakeChain;

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>Factory for creating loggers.</summary>
        protected readonly ILoggerFactory loggerFactory;

        private readonly ContractStateRepositoryRoot stateRoot;

        private readonly SmartContractDecompiler smartContractDecompiler;

        private readonly SmartContractValidator smartContractValidator;

        private readonly ISmartContractGasInjector gasInjector;

        private readonly CoinView coinView;

        public SmartContractAssemblerFactory(
            IConsensusLoop consensusLoop,
            Network network,
            MempoolSchedulerLock mempoolScheduler,
            ITxMempool mempool,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory,
            ContractStateRepositoryRoot stateRoot,
            SmartContractDecompiler smartContractDecompiler,
            SmartContractValidator smartContractValidator,
            ISmartContractGasInjector gasInjector,
            CoinView coinView,
            StakeChain stakeChain = null)
        {
            this.consensusLoop = consensusLoop;
            this.network = network;
            this.mempoolScheduler = mempoolScheduler;
            this.mempool = mempool;
            this.dateTimeProvider = dateTimeProvider;
            this.stateRoot = stateRoot;
            this.smartContractDecompiler = smartContractDecompiler;
            this.smartContractValidator = smartContractValidator;
            this.gasInjector = gasInjector;
            this.coinView = coinView;
            this.stakeChain = stakeChain;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public BlockAssembler Create(ChainedBlock chainTip, AssemblerOptions options = null)
        {
            return new SmartContractBlockAssembler(
                this.consensusLoop,
                this.network,
                this.mempoolScheduler,
                this.mempool,
                this.dateTimeProvider,
                chainTip,
                this.loggerFactory,
                this.stateRoot,
                this.smartContractDecompiler,
                this.smartContractValidator,
                this.gasInjector,
                this.coinView,
                options);
        }
    }
}
