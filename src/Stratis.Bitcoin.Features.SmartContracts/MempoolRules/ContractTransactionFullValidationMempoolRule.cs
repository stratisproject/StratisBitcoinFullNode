using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.SmartContracts.Rules;

namespace Stratis.Bitcoin.Features.SmartContracts.MempoolRules
{
    public class ContractTransactionFullValidationMempoolRule : MempoolRule
    {
        private readonly IEnumerable<IContractTransactionFullValidationRule> internalRules;

        public ContractTransactionFullValidationMempoolRule(Network network,
            ITxMempool mempool,
            MempoolSettings mempoolSettings,
            ChainIndexer chainIndexer,
            IEnumerable<IContractTransactionFullValidationRule> txFullValidationRules,
            ILoggerFactory loggerFactory) : base(network, mempool, mempoolSettings, chainIndexer, loggerFactory)
        {
            this.internalRules = txFullValidationRules;
        }

        public override void CheckTransaction(MempoolValidationContext context)
        {
            // TODO: Cannot do this statically, what is the best approach? Can the transaction checker be injected?
            ContractTransactionFullValidationRule.transactionChecker.CheckTransaction(context, this.internalRules);
        }
    }
}