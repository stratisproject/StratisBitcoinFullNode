using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.BlockPulling;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;

namespace Stratis.Bitcoin.Features.SmartContracts.Consensus
{
    /// <summary>
    /// Extension of consensus rules that provide access to a store based on UTXO (Unspent transaction outputs).
    /// </summary>
    public sealed class SmartContractPowConsensusRuleEngine : PowConsensusRules, ISmartContractCoinviewRule
    {
        public ISmartContractExecutorFactory ExecutorFactory { get; private set; }
        public ContractStateRepositoryRoot OriginalStateRoot { get; private set; }
        public ISmartContractReceiptStorage ReceiptStorage { get; private set; }

        public SmartContractPowConsensusRuleEngine(
            ConcurrentChain chain,
            ICheckpoints checkpoints,
            ConsensusSettings consensusSettings,
            IDateTimeProvider dateTimeProvider,
            ISmartContractExecutorFactory executorFactory,
            ILoggerFactory loggerFactory,
            Network network,
            NodeDeployments nodeDeployments,
            ContractStateRepositoryRoot originalStateRoot,
            ILookaheadBlockPuller puller,
            ICoinView utxoSet,
            ISmartContractReceiptStorage receiptStorage)
            : base(network, loggerFactory, dateTimeProvider, chain, nodeDeployments, consensusSettings, checkpoints, utxoSet, puller)
        {
            this.ExecutorFactory = executorFactory;
            this.OriginalStateRoot = originalStateRoot;
            this.ReceiptStorage = receiptStorage;
        }

        /// <inheritdoc />
        public override RuleContext CreateRuleContext(ValidationContext validationContext, ChainedHeader consensusTip)
        {
            return new PowRuleContext(validationContext, this.Network.Consensus, consensusTip, this.DateTimeProvider.GetTimeOffset());
        }
    }
}