using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.SmartContracts.Rules;
using Stratis.SmartContracts.CLR;

namespace Stratis.Bitcoin.Features.SmartContracts.MempoolRules
{
    public class ContractTransactionFullValidationMempoolRule : MempoolRule
    {
        private readonly ContractTransactionChecker transactionChecker;
        private readonly IEnumerable<IContractTransactionFullValidationRule> internalRules;

        public ContractTransactionFullValidationMempoolRule(Network network,
            ITxMempool mempool,
            MempoolSettings mempoolSettings,
            ChainIndexer chainIndexer,
            ICallDataSerializer serializer,
            IEnumerable<IContractTransactionFullValidationRule> txFullValidationRules,
            ILoggerFactory loggerFactory) : base(network, mempool, mempoolSettings, chainIndexer, loggerFactory)
        {
            this.transactionChecker = new ContractTransactionChecker(serializer);
            this.internalRules = txFullValidationRules;
        }

        public override void CheckTransaction(MempoolValidationContext context)
        {
            this.transactionChecker.CheckTransaction(context, this.internalRules);
        }
    }
}