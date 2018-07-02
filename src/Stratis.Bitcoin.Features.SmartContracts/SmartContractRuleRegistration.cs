using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public sealed class SmartContractRuleRegistration : IRuleRegistration
    {
        private readonly Func<CoinView> coinView;
        private readonly Func<ISmartContractExecutorFactory> executorFactory;
        private readonly IFullNodeBuilder fullNodeBuilder;
        private readonly Func<ILoggerFactory> loggerFactory;
        private readonly Func<ContractStateRepositoryRoot> originalStateRoot;
        private readonly Func<ISmartContractReceiptStorage> receiptStorage;

        public SmartContractRuleRegistration(IFullNodeBuilder fullNodeBuilder)
        {
            this.fullNodeBuilder = fullNodeBuilder;

            this.coinView = () => this.fullNodeBuilder.ServiceProvider.GetService(typeof(CoinView)) as CoinView;
            this.executorFactory = () => this.fullNodeBuilder.ServiceProvider.GetService(typeof(ISmartContractExecutorFactory)) as ISmartContractExecutorFactory;
            this.loggerFactory = () => this.fullNodeBuilder.ServiceProvider.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
            this.originalStateRoot = () => this.fullNodeBuilder.ServiceProvider.GetService(typeof(ContractStateRepositoryRoot)) as ContractStateRepositoryRoot;
            this.receiptStorage = () => this.fullNodeBuilder.ServiceProvider.GetService(typeof(ISmartContractReceiptStorage)) as ISmartContractReceiptStorage;
        }

        public IEnumerable<ConsensusRule> GetRules()
        {
            var rules = new List<ConsensusRule>
            {
                new BlockHeaderRule(),

                // rules that are inside the method CheckBlockHeader
                new CalculateWorkRule(),

                // rules that are inside the method ContextualCheckBlockHeader
                new CheckpointsRule(),
                new AssumeValidRule(),
                new BlockHeaderPowContextualRule(),

                // rules that are inside the method ContextualCheckBlock
                new TransactionLocktimeActivationRule(), // implements BIP113
                new CoinbaseHeightActivationRule(), // implements BIP34
                new WitnessCommitmentsRule(), // BIP141, BIP144
                new BlockSizeRule(),

                // rules that are inside the method CheckBlock
                new BlockMerkleRootRule(),
                new EnsureCoinbaseRule(),
                new CheckPowTransactionRule(),
                new CheckSigOpsRule(),

                // rules that require the store to be loaded (coinview)
                new SmartContractLoadCoinviewRule(),
                new TransactionDuplicationActivationRule(), // implements BIP30

                // Smart contract specific rules
                new TxOutSmartContractExecRule(),
                new OpSpendRule(),
                new SmartContractCoinviewRule(this.coinView(), this.executorFactory(), this.loggerFactory(), this.originalStateRoot(), this.receiptStorage()), // implements BIP68, MaxSigOps and BlockReward calculation
            };

            return rules;
        }
    }
}