using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules;
using Stratis.Bitcoin.Features.SmartContracts.Rules;
using Stratis.SmartContracts.CLR;

namespace Stratis.Bitcoin.Features.SmartContracts.MempoolRules
{
    public class ContractTransactionPartialValidationMempoolRule : MempoolRule
    {
        private readonly ContractTransactionChecker transactionChecker;

        public ContractTransactionPartialValidationMempoolRule(Network network,
            ITxMempool mempool,
            MempoolSettings mempoolSettings,
            ChainIndexer chainIndexer,
            ICallDataSerializer serializer,
            ILoggerFactory loggerFactory) : base(network, mempool, mempoolSettings, chainIndexer, loggerFactory)
        {
            this.transactionChecker = new ContractTransactionChecker(serializer);
        }

        public override void CheckTransaction(MempoolValidationContext context)
        {
            // TODO: Need to somehow inject the rule list too; this is different to the full validation case in that not every partial validation was being applied in the validator originally e.g. ContractSignedCodeLogic?
            this.transactionChecker.CheckTransaction(context, new List<IContractTransactionPartialValidationRule>(){ new SmartContractFormatLogic() });
        }
    }
}