using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.MemoryPool;
using Block = NBitcoin.Block;

namespace Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules
{
    /// <summary>
    /// Validates that no satoshis were supplied in the smart contract create transaction
    /// </summary>
    [ValidationRule(CanSkipValidation = false)]
    public class OpCreateZeroValueRule : ConsensusRule, ISmartContractMempoolRule
    {
        public override Task RunAsync(RuleContext context)
        {
            Block block = context.ValidationContext.Block;

            foreach (Transaction transaction in block.Transactions)
            {
                CheckTransaction(transaction);
            }

            return Task.CompletedTask;
        }

        public void CheckTransaction(MempoolValidationContext context)
        {
            CheckTransaction(context.Transaction);
        }

        private void CheckTransaction(Transaction transaction)
        {
            if (!transaction.IsSmartContractCreateTransaction())
                return;

            // This should never be null as every tx in smartContractTransactions contains a SmartContractOutput
            // So throw if null, because we really didn't expect that
            TxOut smartContractOutput = transaction.Outputs.First(txOut => txOut.ScriptPubKey.IsSmartContractExec);

            if (smartContractOutput.Value != 0)
            {
                this.Throw();
            }
        }

        private void Throw()
        {
            new ConsensusError("op-create-had-nonzero-value", "op create had nonzero value").Throw();
        }
    }
}