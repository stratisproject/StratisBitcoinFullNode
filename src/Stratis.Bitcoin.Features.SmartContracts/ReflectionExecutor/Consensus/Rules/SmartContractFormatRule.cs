using System.Linq;
using System.Threading.Tasks;
using CSharpFunctionalExtensions;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.SmartContracts.CLR;

namespace Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Consensus.Rules
{
    /// <summary>
    /// Validates that a smart contract transaction can be deserialized correctly, and that it conforms to gas
    /// price and gas limit rules.
    /// </summary>
    public class SmartContractFormatRule : FullValidationConsensusRule, ISmartContractMempoolRule
    {
        public const ulong GasLimitMaximum = 100_000;

        public const ulong GasLimitCallMinimum = GasPriceList.BaseCost;

        public const ulong GasLimitCreateMinimum = GasPriceList.CreateCost;

        public const ulong GasPriceMinimum = 1;

        public const ulong GasPriceMaximum = 10_000;

        private readonly ICallDataSerializer callDataSerializer;

        public SmartContractFormatRule(ICallDataSerializer callDataSerializer)
        {
            this.callDataSerializer = callDataSerializer;
        }

        public override Task RunAsync(RuleContext context)
        {
            Block block = context.ValidationContext.BlockToValidate;

            // Check all transactions. We rely on other rules to determine which
            // transactions are allowed to contain SmartContractExec opcodes.
            foreach (Transaction transaction in block.Transactions)
            {
                this.CheckTransaction(transaction, null);
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

            TxOut scTxOut = transaction.TryGetSmartContractTxOut();

            if (scTxOut == null)
            {
                new ConsensusError("no-smart-contract-tx-out", "No smart contract TxOut").Throw();
            }

            // CallDataSerializer swallows all exceptions, so we do not wrap this in a try-catch.
            Result<ContractTxData> callDataDeserializationResult = this.callDataSerializer.Deserialize(scTxOut.ScriptPubKey.ToBytes());

            if (callDataDeserializationResult.IsFailure)
            {
                new ConsensusError("invalid-calldata-format", string.Format("Invalid {0} format", typeof(ContractTxData).Name)).Throw();
            }

            ContractTxData callData = callDataDeserializationResult.Value;

            if (callData.GasPrice < GasPriceMinimum)
            {
                // Supplied gas price is too low.
                this.ThrowGasPriceLessThanMinimum();
            }

            if (callData.GasPrice > GasPriceMaximum)
            {
                // Supplied gas price is too high.
                this.ThrowGasPriceMoreThanMaximum();
            }

            if (callData.IsCreateContract && callData.GasLimit < GasLimitCreateMinimum)
            {
                this.ThrowGasLessThenCreateFee();
            }

            if (!callData.IsCreateContract && callData.GasLimit < GasLimitCallMinimum)
            {
                // Supplied gas limit is too low.
                this.ThrowGasLessThanBaseFee();
            }

            if (callData.GasLimit > GasLimitMaximum)
            {
                // Supplied gas limit is too high - at a certain point we deem that a contract is taking up too much time.
                this.ThrowGasGreaterThanHardLimit();
            }

            // Only measure budget when coming from mempool - this happens inside SmartContractCoinviewRule instead as part of the block.
            if (suppliedBudget != null)
            {
                // Note carrier.GasCostBudget cannot overflow given values are within constraints above.
                if (suppliedBudget < new Money(callData.GasCostBudget))
                {
                    // Supplied satoshis are less than the budget we said we had for the contract execution
                    SmartContractConsensusErrors.FeeTooSmallForGas.Throw();
                }
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
            new ConsensusError("gas-limit-less-than-base-fee", "gas limit supplied is less than the base fee for contract execution: " + GasLimitCallMinimum).Throw();
        }
        private void ThrowGasLessThenCreateFee()
        {
            // TODO make nicer
            new ConsensusError("gas-limit-less-than-create-fee", "gas limit supplied is less than the base fee for contract creation: " + GasLimitCreateMinimum).Throw();
        }

        private void ThrowGasGreaterThanHardLimit()
        {
            // TODO make nicer
            new ConsensusError("total-gas-value-greater-than-hard-limit", "total supplied gas value was greater than our hard limit of " + GasLimitMaximum).Throw();
        }
    }
}