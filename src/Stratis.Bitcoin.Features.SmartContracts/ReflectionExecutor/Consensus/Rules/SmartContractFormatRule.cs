using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.SmartContracts.Core;
using Stratis.SmartContracts.Executor.Reflection;
using Block = NBitcoin.Block;

namespace Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules
{
    /// <summary>
    /// Validates that the supplied transaction satoshis are greater than the gas budget satoshis in the contract invocation
    /// </summary>
    [ValidationRule(CanSkipValidation = false)]
    public class SmartContractFormatRule : UtxoStoreConsensusRule, ISmartContractMempoolRule
    {
        public const ulong GasLimitMaximum = 5_000_000;

        public const ulong GasLimitMinimum = GasPriceList.BaseCost;

        public const ulong GasPriceMinimum = 1;

        public const ulong GasPriceMaximum = 10_000;

        public SmartContractFormatRule()
        {
        }

        public override Task RunAsync(RuleContext context)
        {
            Block block = context.ValidationContext.Block;

            foreach (Transaction transaction in block.Transactions.Where(x => !x.IsCoinBase && !x.IsCoinStake))
            {
                if (!transaction.IsSmartContractExecTransaction())
                    return Task.CompletedTask;

                Money transactionFee = transaction.GetFee(((UtxoRuleContext)context).UnspentOutputSet);

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

            // TODO: What if deserialization throws an error? We should check this.
            // Also the deserializer should throw custom exceptions.
            SmartContractCarrier carrier = SmartContractCarrier.Deserialize(transaction);

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

            // Note carrier.GasCostBudget cannot overflow given values are within constraints above.
            if (suppliedBudget < new Money(carrier.GasCostBudget))
            {
                // Supplied satoshis are less than the budget we said we had for the contract execution
                this.ThrowGasGreaterThanFee();
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