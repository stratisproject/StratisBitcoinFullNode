using System;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Core.Backend;
using Block = NBitcoin.Block;

namespace Stratis.Bitcoin.Features.SmartContracts.Consensus.Rules
{
    /// <summary>
    /// Validates that the supplied transaction satoshis are greater than the gas budget satoshis in the contract invocation
    /// </summary>
    [ValidationRule(CanSkipValidation = false)]
    public class GasBudgetRule : UtxoStoreConsensusRule, ISmartContractMempoolRule
    {
        public const ulong GasLimitMaximum = 5_000_000;

        public const ulong GasLimitMinimum = GasPriceList.BaseCost;

        public const ulong GasPriceMinimum = 1;

        public const ulong GasPriceMaximum = 10_000;

        public override Task RunAsync(RuleContext context)
        {
            Block block = context.BlockValidationContext.Block;

            foreach (Transaction transaction in block.Transactions.Where(x => !x.IsCoinBase && !x.IsCoinStake))
            {
                if (!transaction.IsSmartContractExecTransaction())
                    return Task.CompletedTask;

                Money transactionFee = transaction.GetFee(context.Set);

                CheckTransaction(transaction, transactionFee);
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

            if (carrier.GasPrice < GasPriceMinimum)
            {
                // Supplied gas price is too low.
                this.ThrowGasPriceLessThanMinimum();
            }

            if (carrier.GasPrice > GasPriceMaximum)
            {
                // Supplied gas price is too high.
                this.ThrowGasPriceMoreThanMaximum();
            }

            if (carrier.GasLimit < GasLimitMinimum)
            {
                // Supplied gas limit is too low.
                this.ThrowGasLessThanBaseFee();
            }


            if (carrier.GasLimit > GasLimitMaximum)
            {
                // Supplied gas limit is too high - at a certain point we deem that a contract is taking up too much time. 
                this.ThrowGasGreaterThanHardLimit();
            }

            try
            {
                if (suppliedBudget < new Money(carrier.GasCostBudget))
                {
                    // Supplied satoshis are less than the budget we said we had for the contract execution
                    this.ThrowGasGreaterThanFee();
                }
            }
            catch (OverflowException)
            {
                this.ThrowGasOverflowException();
            }
        }

        private void ThrowGasPriceLessThanMinimum()
        {
            // TODO make nicer
            new ConsensusError("gas-price-less-than-minimum", "gas price supplied is less than minimum allowed: " + GasPriceMinimum).Throw();
        }

        private void ThrowGasPriceMoreThanMaximum()
        {
            // TODO make nicer
            new ConsensusError("gas-price-more-than-maximum", "gas price supplied is more than maximum allowed: " + GasPriceMaximum).Throw();
        }

        private void ThrowGasLessThanBaseFee()
        {
            // TODO make nicer
            new ConsensusError("gas-limit-less-than-base-fee", "gas limit supplied is less than the base fee for contract execution: " + GasLimitMinimum).Throw();
        }

        private void ThrowGasOverflowException()
        {
            // TODO make nicer
            new ConsensusError("gas-overflow", "gasLimit * gasPrice caused a ulong overflow").Throw();
        }

        private void ThrowGasGreaterThanHardLimit()
        {
            // TODO make nicer
            new ConsensusError("total-gas-value-greater-than-hard-limit", "total supplied gas value was greater than our hard limit of " + GasLimitMaximum).Throw();
        }

        private void ThrowGasGreaterThanFee()
        {
            // TODO make nicer
            new ConsensusError("total-gas-value-greater-than-total-fee", "total supplied gas value was greater than total supplied fee value").Throw();
        }
    }
}