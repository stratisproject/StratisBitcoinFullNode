using System;
using System.Collections.Generic;
using System.Linq;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules;
using Stratis.Bitcoin.Utilities;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.State;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public sealed class SmartContractRuleRegistration : IAdditionalRuleRegistration
    {
        private IRuleRegistration baseRegistration;
        private Func<CoinView> coinView;
        private Func<ISmartContractExecutorFactory> executorFactory;
        private readonly IFullNodeBuilder fullNodeBuilder;
        private Func<ContractStateRepositoryRoot> originalStateRoot;

        public SmartContractRuleRegistration(IFullNodeBuilder fullNodeBuilder)
        {
            this.fullNodeBuilder = fullNodeBuilder;
        }

        public void SetPreviousRegistration(IRuleRegistration previousRegistration)
        {
            this.baseRegistration = previousRegistration;

            this.coinView = () => this.fullNodeBuilder.ServiceProvider.GetService(typeof(CoinView)) as CoinView;
            this.executorFactory = () => this.fullNodeBuilder.ServiceProvider.GetService(typeof(ISmartContractExecutorFactory)) as ISmartContractExecutorFactory;
            this.originalStateRoot = () => this.fullNodeBuilder.ServiceProvider.GetService(typeof(ContractStateRepositoryRoot)) as ContractStateRepositoryRoot;
        }

        public IEnumerable<ConsensusRule> GetRules()
        {
            Guard.Assert(this.baseRegistration != null);

            var rules = new List<ConsensusRule>();

            rules.AddRange(this.baseRegistration.GetRules().Where(r => r.GetType() != typeof(PowCoinviewRule)));

            rules.Add(new TxOutSmartContractExecRule());
            rules.Add(new OpSpendRule());
            rules.Add(new OpCreateZeroValueRule());
            rules.Add(new SmartContractCoinviewRule(this.coinView(), this.executorFactory(), this.originalStateRoot()));

            return rules;
        }
    }
}
