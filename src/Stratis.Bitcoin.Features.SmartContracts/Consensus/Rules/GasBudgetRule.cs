using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.SmartContracts.Core;
using Block = NBitcoin.Block;

namespace Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules
{
    /// <summary>
    /// Validates that the supplied transaction satoshis are greater than the gas budget satoshis in the contract invocation
    /// </summary>
    [ValidationRule(CanSkipValidation = false)]
    public class GasBudgetRule : ConsensusRule, ISmartContractMempoolRule
    {
        public const ulong HardGasLimit = 5_000_000;

        public override Task RunAsync(RuleContext context)
        {
            Block block = context.BlockValidationContext.Block;

            foreach(Transaction transaction in block.Transactions.Where(x=> !x.IsCoinBase && !x.IsCoinStake))
            {
                CheckTransaction(transaction, transaction.GetFee(context.Set));
            }

            return Task.CompletedTask;
        }

        public void CheckTransaction(MempoolValidationContext context)
        {
            CheckTransaction(context.Transaction, context.Fees);
        }

        private void CheckTransaction(Transaction transaction, Money suppliedBudget)
        {
            if (!transaction.IsSmartContractExecTransaction())
                return;

            // This should never be null as every tx in smartContractTransactions contains a SmartContractOutput
            // So throw if null, because we really didn't expect that
            TxOut smartContractOutput = transaction.Outputs.First(txOut => txOut.ScriptPubKey.IsSmartContractExec);

            var carrier = SmartContractCarrier.Deserialize(transaction, smartContractOutput);

            if (carrier.GasLimit > HardGasLimit)
            {
                // Supplied gas limit is too high - at a certain point we deem that a contract is taking up too much time. 
                this.ThrowGasGreaterThanHardLimit();
            }


            if (suppliedBudget < new Money(carrier.GasCostBudget))
            {
                // Supplied satoshis are less than the budget we said we had for the contract execution
                this.ThrowGasGreaterThanFee();
            }
        }

        private void ThrowGasGreaterThanHardLimit()
        {
            // TODO make nicer
            new ConsensusError("total-gas-value-greater-than-hard-limit", "total supplied gas value was greater than our hard limit of " + HardGasLimit).Throw();
        }

        private void ThrowGasGreaterThanFee()
        {
            // TODO make nicer
            new ConsensusError("total-gas-value-greater-than-total-fee", "total supplied gas value was greater than total supplied fee value").Throw();
        }
    }
}