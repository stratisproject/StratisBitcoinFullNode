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
        /// Can be checked before loading coinview.
        /// </summary>
        private static readonly List<ISmartContractMempoolRule> preTxRules = new List<ISmartContractMempoolRule>
        {
            new OpCreateZeroValueRule(),
            new MempoolOpSpendRule(),
            new TxOutSmartContractExecRule()
        };

        /// <summary>
        /// Rely on coinview to be loaded.
        /// </summary>
        private static readonly List<ISmartContractMempoolRule> feeTxRules = new List<ISmartContractMempoolRule>
        {
            new GasBudgetRule()
        };

        public SmartContractMempoolValidator(ITxMempool memPool, MempoolSchedulerLock mempoolLock, IPowConsensusValidator consensusValidator, IDateTimeProvider dateTimeProvider, MempoolSettings mempoolSettings, ConcurrentChain chain, CoinView coinView, ILoggerFactory loggerFactory, NodeSettings nodeSettings) : base(memPool, mempoolLock, consensusValidator, dateTimeProvider, mempoolSettings, chain, coinView, loggerFactory, nodeSettings)
        {
        }

        protected override void PreMempoolChecks(MempoolValidationContext context)
        {
            base.PreMempoolChecks(context);
            foreach (ISmartContractMempoolRule rule in preTxRules)
            {
                rule.CheckTransaction(context);
            }
        }

        protected override void CheckFee(MempoolValidationContext context)
        {
            base.CheckFee(context);
            foreach (ISmartContractMempoolRule rule in feeTxRules)
            {
                rule.CheckTransaction(context);
            }
        }
    }
}
