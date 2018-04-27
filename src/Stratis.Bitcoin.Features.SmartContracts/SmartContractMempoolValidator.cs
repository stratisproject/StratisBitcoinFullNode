using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Interfaces;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public class SmartContractMempoolValidator : MempoolValidator
    {
        /// <summary>
        /// These could all be one rule in the future if we add rules in the future that aren't so transaction-centric. Kind of like the CheckPoWTransactionRule
        /// </summary>
        /// 
        private static readonly List<ISmartContractMempoolRule> txRules = new List<ISmartContractMempoolRule>
        {
            new GasBudgetRule(),
            new OpCreateZeroValueRule(),
            new MempoolOpSpendRule(),
            new TxOutSmartContractExecRule()
        };

        public SmartContractMempoolValidator(ITxMempool memPool, MempoolSchedulerLock mempoolLock, IPowConsensusValidator consensusValidator, IDateTimeProvider dateTimeProvider, MempoolSettings mempoolSettings, ConcurrentChain chain, CoinView coinView, ILoggerFactory loggerFactory, NodeSettings nodeSettings) : base(memPool, mempoolLock, consensusValidator, dateTimeProvider, mempoolSettings, chain, coinView, loggerFactory, nodeSettings)
        {
        }

        protected override void PreMempoolChecks(MempoolValidationContext context)
        {
            foreach (ISmartContractMempoolRule rule in txRules)
            {
                rule.CheckTransaction(context);
            }
            base.PreMempoolChecks(context);
        }
    }
}
