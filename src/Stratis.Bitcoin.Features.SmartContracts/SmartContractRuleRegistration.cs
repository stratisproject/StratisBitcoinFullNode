using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Receipts;
using Stratis.SmartContracts.Core.State;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public sealed class SmartContractRuleRegistration : IAdditionalRuleRegistration
    {
        private IRuleRegistration baseRegistration;
        private Func<CoinView> coinView;
        private Func<ISmartContractExecutorFactory> executorFactory;
        private readonly IFullNodeBuilder fullNodeBuilder;
        private Func<ILoggerFactory> loggerFactory;
        private Func<ContractStateRepositoryRoot> originalStateRoot;
        private Func<ISmartContractReceiptStorage> receiptStorage;

        public SmartContractRuleRegistration(IFullNodeBuilder fullNodeBuilder)
        {
            this.fullNodeBuilder = fullNodeBuilder;
        }

        public void SetPreviousRegistration(IRuleRegistration previousRegistration)
        {
            this.baseRegistration = previousRegistration;

            this.coinView = () => this.fullNodeBuilder.ServiceProvider.GetService(typeof(CoinView)) as CoinView;
            this.executorFactory = () => this.fullNodeBuilder.ServiceProvider.GetService(typeof(ISmartContractExecutorFactory)) as ISmartContractExecutorFactory;
            this.loggerFactory = () => this.fullNodeBuilder.ServiceProvider.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
            this.originalStateRoot = () => this.fullNodeBuilder.ServiceProvider.GetService(typeof(ContractStateRepositoryRoot)) as ContractStateRepositoryRoot;
            this.receiptStorage = () => this.fullNodeBuilder.ServiceProvider.GetService(typeof(ISmartContractReceiptStorage)) as ISmartContractReceiptStorage;
        }

        public IEnumerable<ConsensusRule> GetRules()
        {
            Guard.Assert(this.baseRegistration != null);

            var rules = new List<ConsensusRule>();

            rules.AddRange(this.baseRegistration.GetRules().Where(r => r.GetType() != typeof(PowCoinviewRule)));

            rules.Add(new TxOutSmartContractExecRule());
            rules.Add(new OpSpendRule());
            rules.Add(new SmartContractCoinviewRule(this.coinView(), this.executorFactory(), this.loggerFactory(), this.originalStateRoot(), this.receiptStorage()));

            return rules;
        }
    }
}