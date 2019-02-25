using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Util;

namespace Stratis.Bitcoin.Features.SmartContracts.Rules
{
    public class CanGetSenderRule : UtxoStoreConsensusRule, ISmartContractMempoolRule
    {
        private readonly ISenderRetriever senderRetriever;

        public CanGetSenderRule(ISenderRetriever senderRetriever)
        {
            this.senderRetriever = senderRetriever;
        }

        public override Task RunAsync(RuleContext context)
        {
            Block block = context.ValidationContext.BlockToValidate;
            IList<Transaction> processedTxs = new List<Transaction>();

            foreach (Transaction transaction in block.Transactions)
            {
                this.CheckTransactionInsideBlock(transaction, this.PowParent.UtxoSet, processedTxs);
                processedTxs.Add(transaction);
            }

            return Task.CompletedTask;
        }

        private void CheckTransactionInsideBlock(Transaction transaction, ICoinView coinView, IList<Transaction> blockTxs)
        {
            // If wanting to execute a contract, we must be able to get the sender.
            if (transaction.Outputs.Any(x => x.ScriptPubKey.IsSmartContractExec()))
            {
                GetSenderResult result = this.senderRetriever.GetSender(transaction, coinView, blockTxs);
                if (!result.Success)
                    new ConsensusError("cant-get-sender", "smart contract output without a P2PKH as the first input to the tx.").Throw();
            }
        }

        public void CheckTransaction(MempoolValidationContext context)
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


