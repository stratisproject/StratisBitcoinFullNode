using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Bitcoin.Features.SmartContracts.MempoolRules
{
    public class CanGetSenderMempoolRule : MempoolRule
    {
        private readonly ISenderRetriever senderRetriever;

        public CanGetSenderMempoolRule(Network network,
            ITxMempool mempool,
            MempoolSettings mempoolSettings,
            ChainIndexer chainIndexer,
            ISenderRetriever senderRetriever,
            ILoggerFactory loggerFactory) : base(network, mempool, mempoolSettings, chainIndexer, loggerFactory)
        {
            this.senderRetriever = senderRetriever;
        }

        /// <inheritdoc/>
        public override void CheckTransaction(MempoolValidationContext context)
        {
            // If wanting to execute a contract, we must be able to get the sender.
            if (context.Transaction.Outputs.Any(x => x.ScriptPubKey.IsSmartContractExec()))
            {
                GetSenderResult result = this.senderRetriever.GetSender(context.Transaction, context.View);
                if (!result.Success)
                    new ConsensusError("cant-get-sender", "smart contract output without a P2PKH as the first input to the tx.").Throw();
            }
        }
    }
}